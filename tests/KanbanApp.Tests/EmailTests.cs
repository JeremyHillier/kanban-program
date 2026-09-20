using System.IO;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The default-email-app fallback for Email This Task (new Outlook, other mail apps, no Outlook).
public sealed class EmailTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CardViewModel Card(Action<CardItem>? configure = null)
    {
        var model = new CardItem { Title = "Quarterly report", Priority = "High" };
        configure?.Invoke(model);
        return new CardViewModel(model) { ProjectName = "Finance", GoalName = "No Goal" };
    }

    [Fact]
    public void MailtoLink_EscapesTheSubjectAndBodyButKeepsTheAddressReadable()
    {
        var uri = OutlookEmailHelper.BuildMailtoUri("pat.lee@example.com", "Task: Q&A prep", "Line one\r\nLine two & more");

        Assert.StartsWith("mailto:pat.lee@example.com?subject=", uri);
        Assert.Contains("subject=Task%3A%20Q%26A%20prep", uri);
        Assert.Contains("&body=Line%20one%0D%0ALine%20two%20%26%20more", uri);
    }

    [Fact]
    public void MailtoLink_LeavesAShortBodyWhole()
    {
        var body = "Short body";
        var uri = OutlookEmailHelper.BuildMailtoUri("a@b.com", "Task", body);
        Assert.EndsWith("body=Short%20body", uri);
    }

    [Fact]
    public void MailtoLink_ShortensALongBodyToFitAndMarksTheCut()
    {
        var body = string.Join("\r\n", Enumerable.Range(1, 400).Select(i => $"Note line {i} with some words"));

        var uri = OutlookEmailHelper.BuildMailtoUri("a@b.com", "Task: long", body);

        Assert.True(uri.Length <= OutlookEmailHelper.MaxMailtoLength, $"length {uri.Length}");
        Assert.EndsWith("%0D%0A%E2%80%A6", uri); // "\r\n…"
        Assert.Contains("Note%20line%201%20", uri);
    }

    [Fact]
    public void MailtoLink_NeverSplitsAnEmojiWhenShortening()
    {
        var body = string.Concat(Enumerable.Repeat("😀", 2000));

        var uri = OutlookEmailHelper.BuildMailtoUri("a@b.com", "Task", body);

        Assert.True(uri.Length <= OutlookEmailHelper.MaxMailtoLength);
        Assert.DoesNotContain("%EF%BF%BD", uri); // the replacement character a split pair turns into
    }

    [Theory]
    [InlineData("Report: Q3/Q4?", "Report_ Q3_Q4_")]
    [InlineData("Plain title", "Plain title")]
    [InlineData("Ends with dots...", "Ends with dots")]
    [InlineData("???", "___")]
    [InlineData("   ", "Task")]
    [InlineData("...", "Task")]
    public void SanitizeFileName_MakesASafeNonEmptyName(string title, string expected) =>
        Assert.Equal(expected, OutlookEmailHelper.SanitizeFileName(title));

    [Fact]
    public void SanitizeFileName_CapsLongTitles() =>
        Assert.Equal(60, OutlookEmailHelper.SanitizeFileName(new string('x', 200)).Length);

    [Fact]
    public void AttachmentFolder_HoldsTheExcelFileAndCopiesOfTheAttachments()
    {
        var root = _temp.File("email");
        var sourceA = _temp.File("notes.txt");
        var otherDir = Directory.CreateDirectory(_temp.File("other")).FullName;
        var sourceB = Path.Combine(otherDir, "notes.txt"); // same file name as sourceA
        File.WriteAllText(sourceA, "A");
        File.WriteAllText(sourceB, "B");

        var folder = OutlookEmailHelper.PrepareAttachmentFolder(root, "Quarterly report",
            new ImportedTaskRow { Title = "Quarterly report" }, [sourceA, sourceB, _temp.File("missing.pdf")]);

        var names = Directory.GetFiles(folder).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["KanbanTask_Quarterly report.xlsx", "notes (2).txt", "notes.txt"], names);
        Assert.Equal("Quarterly report", Assert.Single(ImportService.ReadTasks(Path.Combine(folder, "KanbanTask_Quarterly report.xlsx"))).Title);
        Assert.True(File.Exists(sourceA), "the original attachment must be left in place");
    }

    [Fact]
    public void AttachmentFolder_IsRecreatedEachTime()
    {
        var root = _temp.File("email");
        var folder = OutlookEmailHelper.PrepareAttachmentFolder(root, "Task A", new ImportedTaskRow { Title = "Task A" }, []);
        File.WriteAllText(Path.Combine(folder, "left over.txt"), "old");

        OutlookEmailHelper.PrepareAttachmentFolder(root, "Task A", new ImportedTaskRow { Title = "Task A" }, []);

        Assert.False(File.Exists(Path.Combine(folder, "left over.txt")));
    }

    [Fact]
    public void AttachmentFolder_ForAnUnnamableTitle_NeverClearsTheRootFolder()
    {
        var root = _temp.File("email");
        Directory.CreateDirectory(root);
        var unrelated = Path.Combine(root, "keep me.txt");
        File.WriteAllText(unrelated, "keep");

        OutlookEmailHelper.PrepareAttachmentFolder(root, "...", new ImportedTaskRow { Title = "..." }, []);

        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public void PlainTextBody_IncludesTheTaskDetails()
    {
        var card = Card(m =>
        {
            m.DueDate = new DateTime(2026, 9, 14);
            m.DueTime = "14:30";
            m.Notes = "Check figures\nwith Sam";
        });
        card.GoalName = "Close the books";
        card.Flags = [new FlagViewModel(new Flag { Name = "Urgent" }), new FlagViewModel(new Flag { Name = "Client" })];
        card.SubTasks = [new SubTaskViewModel(new SubTaskItem { Title = "Draft", IsDone = true }), new SubTaskViewModel(new SubTaskItem { Title = "Review" })];

        var body = OutlookEmailHelper.BuildPlainTextBody(card, "Jane Doe\r\nManager");

        Assert.StartsWith("Quarterly report\r\n\r\n", body);
        Assert.Contains("Project: Finance\r\n", body);
        Assert.Contains("Priority: High\r\n", body);
        Assert.Contains("Due: 14-Sep-2026 2:30 PM\r\n", body);
        Assert.Contains("Goal: Close the books\r\n", body);
        Assert.Contains("Flags: Urgent, Client\r\n", body);
        Assert.Contains("Notes:\r\nCheck figures\r\nwith Sam\r\n", body);
        Assert.Contains("[x] Draft\r\n[ ] Review\r\n", body);
        Assert.Contains("click Import Tasks", body);
        Assert.EndsWith("Jane Doe\r\nManager", body);
    }

    [Fact]
    public void PlainTextBody_LeavesOutEmptyFields()
    {
        var body = OutlookEmailHelper.BuildPlainTextBody(Card(), signature: null);

        Assert.DoesNotContain("Due:", body);
        Assert.DoesNotContain("Goal:", body);
        Assert.DoesNotContain("Flags:", body);
        Assert.DoesNotContain("Notes:", body);
        Assert.DoesNotContain("Sub-tasks:", body);
        Assert.EndsWith("your own board.", body);
    }

    [Fact]
    public void PlainTextBody_ShowsADateWithoutATimeWhenNoTimeIsSet()
    {
        var body = OutlookEmailHelper.BuildPlainTextBody(Card(m => m.DueDate = new DateTime(2026, 9, 14)), null);
        Assert.Contains("Due: 14-Sep-2026\r\n", body);
    }
}
