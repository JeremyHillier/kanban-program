using System.IO;
using System.Xml.Linq;
using ClosedXML.Excel;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// A task emailed from the app carries all of itself in the attached Excel file - repeating, time,
// notes, sub-tasks, flags and website - and Import puts all of it back on the recipient's board.
public sealed class WholeTaskFileTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheOneTaskFile_CarriesEveryField_AndReadsThemBack()
    {
        var path = _temp.File("one.xlsx");
        ImportService.SaveSingleTaskFile(path, new ImportedTaskRow
        {
            Title = "Pay the rent",
            DueDate = new DateTime(2026, 11, 1),
            DueTime = "14:30",
            RecurrencePattern = "Monthly",
            RecurrenceCount = 6,
            Flags = "Urgent; Home",
            WebsiteUrl = "landlord.example.com",
            Notes = "Account 1234\nReference: flat 2",
            SubTasks = [("Check balance", true), ("Send transfer", false)]
        });

        var row = Assert.Single(ImportService.ReadTasks(path));
        Assert.Equal("14:30", row.DueTime);
        Assert.Equal("Monthly", row.RecurrencePattern);
        Assert.Equal(6, row.RecurrenceCount);
        Assert.Equal("Urgent; Home", row.Flags);
        Assert.Equal("landlord.example.com", row.WebsiteUrl);
        Assert.Equal("Account 1234\nReference: flat 2", row.Notes);
        Assert.Equal([("Check balance", true), ("Send transfer", false)], row.SubTasks);
    }

    [Fact]
    public void ARepeatingTaskWithNoEnd_HasNoCount()
    {
        var path = _temp.File("one.xlsx");
        ImportService.SaveSingleTaskFile(path, new ImportedTaskRow { Title = "Water plants", RecurrencePattern = "Weekly" });

        var row = Assert.Single(ImportService.ReadTasks(path));
        Assert.Equal("Weekly", row.RecurrencePattern);
        Assert.Null(row.RecurrenceCount);
    }

    // A customer's own spreadsheet: other headings, capitals, an Excel time, a count with no pattern.
    [Fact]
    public void AHandMadeSheet_IsReadForgivingly()
    {
        var path = _temp.File("mine.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            string[] headings = ["Task", "Recurring", "Times", "Time", "Tags", "Description", "Checklist", "Link"];
            for (var i = 0; i < headings.Length; i++) sheet.Cell(1, i + 1).Value = headings[i];

            sheet.Cell(2, 1).Value = "Team meeting";
            sheet.Cell(2, 2).Value = "  weekly ";
            sheet.Cell(2, 3).Value = 4;
            sheet.Cell(2, 4).Value = new TimeSpan(9, 15, 0);
            sheet.Cell(2, 5).Value = "Meetings";
            sheet.Cell(2, 6).Value = "Room B";
            sheet.Cell(2, 7).Value = "- [X] Book room\n\n• agenda";
            sheet.Cell(2, 8).Value = "https://example.com/meet";

            sheet.Cell(3, 1).Value = "One-off";
            sheet.Cell(3, 2).Value = "Every so often";  // not a pattern: a one-off task
            sheet.Cell(3, 3).Value = 3;                 // so the count means nothing
            sheet.Cell(3, 4).Value = "2:30 pm";
            workbook.SaveAs(path);
        }

        var rows = ImportService.ReadTasks(path);

        Assert.Equal("Weekly", rows[0].RecurrencePattern);
        Assert.Equal(4, rows[0].RecurrenceCount);
        Assert.Equal("09:15", rows[0].DueTime);
        Assert.Equal("Meetings", rows[0].Flags);
        Assert.Equal("Room B", rows[0].Notes);
        Assert.Equal([("Book room", true), ("agenda", false)], rows[0].SubTasks);
        Assert.Equal("https://example.com/meet", rows[0].WebsiteUrl);

        Assert.Null(rows[1].RecurrencePattern);
        Assert.Null(rows[1].RecurrenceCount);
        Assert.Equal("14:30", rows[1].DueTime);
        Assert.Empty(rows[1].SubTasks);
        Assert.Null(rows[1].Notes);
    }

    [Fact]
    public void SubTasks_RoundTripThroughOneCell()
    {
        var text = ImportService.FormatSubTasks([("Draft", true), (" Review ", false), ("  ", false)]);
        Assert.Equal("[x] Draft\nReview", text);
        Assert.Equal([("Draft", true), ("Review", false)], ImportService.ParseSubTasks(text));
        Assert.Empty(ImportService.ParseSubTasks(null));
    }

    [Fact]
    public void TheTemplate_HasTheNewColumns_AndARepeatsList()
    {
        var path = _temp.File("template.xlsx");
        ImportService.SaveTemplate(path, ["To Do"], ["General"], [], []);

        using var workbook = new XLWorkbook(path);
        var headings = workbook.Worksheet("Tasks").Row(2).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(["Title", "Category", "Priority", "Project", "Goal", "Due Date", "Who", "Start Date", "Waiting On",
            "Due Time", "Repeats", "Repeat Times", "Flags", "Website", "Notes", "Sub-tasks", "Task ID"], headings);
        Assert.Contains(workbook.Worksheet("Tasks").DataValidations, v => v.Ranges.Any(r => r.FirstColumn().ColumnNumber() == 11));
    }
}

// The list of patterns has to match the task screen's and the date maths.
public class RecurrencePatternTests
{
    [Fact]
    public void ThePatterns_AreTheTaskScreensList_InOrder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        var xaml = XDocument.Load(Path.Combine(dir!.FullName, "src", "KanbanApp", "Views", "AddTaskWindow.xaml"));

        var combo = xaml.Descendants().Single(e => e.Name.LocalName == "ComboBox"
            && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "RecurrenceComboBox"));
        var items = combo.Elements().Where(e => e.Name.LocalName == "ComboBoxItem").Select(e => e.Attribute("Content")!.Value);

        Assert.Equal(RecurrencePatterns.All, items);
    }

    [Theory]
    [InlineData("weekly", "Weekly")]
    [InlineData(" BI-WEEKLY ", "Bi-Weekly")]
    [InlineData("fortnightly", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void APattern_IsFound_WhateverItsCapitals(string? typed, string? expected)
    {
        Assert.Equal(expected, RecurrencePatterns.Find(typed));
    }
}

[Collection(WpfCollection.Name)]
public sealed class WholeTaskImportTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard(string name) => new(new DatabaseService(_temp.File(name)));

    [Fact]
    public void AnEmailedTask_ArrivesWhole_AndKeepsRepeating() => wpf.Run(() =>
    {
        // The sender's task.
        var sender = OpenBoard("sender.db");
        var flag = sender.AddFlag("Urgent").Item!;
        var original = sender.AddCard("Pay the rent", sender.Columns.First(), sender.Projects.First(), "High", new DateTime(2026, 11, 1), null,
            true, "Monthly", null, [flag],
            [new SubTaskViewModel(new SubTaskItem { Title = "Check balance", IsDone = true }), new SubTaskViewModel(new SubTaskItem { Title = "Send transfer" })],
            notes: "Account 1234", websiteUrl: "landlord.example.com", dueTime: "14:30", recurrencesLeft: 6);

        // The file attached to the email, then the recipient importing it.
        var file = _temp.File("KanbanTask.xlsx");
        ImportService.SaveSingleTaskFile(file, OutlookEmailHelper.BuildImportRow(original, sender));
        var recipient = OpenBoard("recipient.db");
        var card = Assert.Single(recipient.ImportCards(ImportService.ReadTasks(file)));

        Assert.Equal("Pay the rent", card.Title);
        Assert.True(card.IsRecurring);
        Assert.Equal("Monthly", card.RecurrencePattern);
        Assert.Equal(6, card.RecurrencesLeft);
        Assert.Equal("14:30", card.DueTime);
        Assert.Equal("Account 1234", card.Notes);
        Assert.Equal("landlord.example.com", card.WebsiteUrl);
        Assert.Equal(["Urgent"], card.Flags.Select(f => f.Name));       // added to the recipient's flags
        Assert.Contains(recipient.Flags, f => f.Name == "Urgent");
        Assert.Equal([("Check balance", true), ("Send transfer", false)], card.SubTasks.Select(s => (s.Title, s.IsDone)));

        // Finished on the recipient's board, it makes next month's, one fewer to go.
        var done = recipient.Columns.Single(c => c.Name == "Done");
        recipient.MoveCardCommand.Execute((card, done));
        var next = recipient.Columns.Single(c => c.Name == "To Do").Cards.Single(c => c.Title == "Pay the rent");
        Assert.Equal(new DateTime(2026, 12, 1), next.DueDate);
        Assert.Equal(5, next.RecurrencesLeft);
    });

    [Fact]
    public void ALinkThatIsNotAWebOrEmailAddress_IsLeftOff_AndATimeNeedsADate() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");

        var cards = board.ImportCards(
        [
            new ImportedTaskRow { Title = "a", WebsiteUrl = @"C:\Windows\System32\calc.exe", DueTime = "09:00" },
            new ImportedTaskRow { Title = "b", WebsiteUrl = "mailto:sam@example.com", DueDate = new DateTime(2026, 10, 9), DueTime = "09:00" },
        ]);

        Assert.Null(cards[0].WebsiteUrl);
        Assert.Null(cards[0].DueTime);                 // no due date, so no time
        Assert.Equal("mailto:sam@example.com", cards[1].WebsiteUrl);
        Assert.Equal("09:00", cards[1].DueTime);
    });

    [Fact]
    public void AnExistingFlag_IsUsed_NotDuplicated() => wpf.Run(() =>
    {
        var board = OpenBoard("board.db");
        board.AddFlag("Urgent");

        var card = Assert.Single(board.ImportCards([new ImportedTaskRow { Title = "a", Flags = "urgent; urgent;  New one " }]));

        Assert.Equal(["Urgent", "New one"], card.Flags.Select(f => f.Name));
        Assert.Single(board.Flags, f => f.Name.Equals("urgent", StringComparison.OrdinalIgnoreCase));
    });
}
