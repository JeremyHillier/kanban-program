using System.IO;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// Dropping things on the Import screen: Excel files as they are, Excel files inside a dropped
// Outlook email, and everything else left out by name.
public sealed class ImportDropTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string ExcelFile(string name, string title)
    {
        var path = _temp.File(name);
        ImportService.SaveSingleTaskFile(path, new ImportedTaskRow { Title = title, Priority = "High", Project = "Website", DueDate = new DateTime(2026, 10, 15) });
        return path;
    }

    // A .msg the way the reader expects it, holding the given files as attachments.
    private string MessageFile(string name, params (string Name, byte[] Bytes)[] attachments)
    {
        var path = _temp.File(name);
        OutlookDragDropHelper.WriteMsgWithAttachments(path, attachments);
        return path;
    }

    [Fact]
    public void AnExcelFile_IsTakenAsItIs_AndOtherFilesAreNamedAsLeftOut()
    {
        var excel = ExcelFile("tasks.xlsx", "Order the parts");
        File.WriteAllText(_temp.File("notes.txt"), "hello");

        var found = ImportDrop.FindExcelFiles([excel, _temp.File("notes.txt")], _temp.File("work"));

        Assert.Equal([excel], found.ExcelFiles);
        Assert.Equal(["notes.txt"], found.Ignored);
    }

    [Fact]
    public void AnEmailDroppedWhole_GivesUpTheExcelFileInsideIt_AndTheTasksReadBack()
    {
        var excel = ExcelFile("KanbanTask_Order the parts.xlsx", "Order the parts");
        var msg = MessageFile("email.msg",
            ("image001.png", [1, 2, 3]),
            ("KanbanTask_Order the parts.xlsx", File.ReadAllBytes(excel)),
            ("minutes.docx", [4, 5]));

        var found = ImportDrop.FindExcelFiles([msg], _temp.File("work"));

        var inside = Assert.Single(found.ExcelFiles);
        Assert.Empty(found.Ignored);
        Assert.Equal("KanbanTask_Order the parts.xlsx", Path.GetFileName(inside));
        Assert.StartsWith(_temp.File("work"), inside);

        var rows = ImportService.ReadTasks(inside);
        var row = Assert.Single(rows);
        Assert.Equal("Order the parts", row.Title);
        Assert.Equal("High", row.Priority);
        Assert.Equal(new DateTime(2026, 10, 15), row.DueDate);
    }

    [Fact]
    public void AnEmailWithNoExcelAttached_IsLeftOut_AndSaysSo()
    {
        var msg = MessageFile("email.msg", ("photo.jpg", [9, 9, 9]));

        var found = ImportDrop.FindExcelFiles([msg], _temp.File("work"));

        Assert.Empty(found.ExcelFiles);
        Assert.Equal(["email.msg (an email with no Excel file attached)"], found.Ignored);
    }

    [Fact]
    public void AFileThatOnlyLooksLikeAnEmail_IsNotAnError()
    {
        File.WriteAllText(_temp.File("fake.msg"), "not a compound file");

        var found = ImportDrop.FindExcelFiles([_temp.File("fake.msg")], _temp.File("work"));

        Assert.Empty(found.ExcelFiles);
        Assert.Single(found.Ignored);
    }

    [Fact]
    public void TwoAttachmentsWithTheSameName_BothSurvive()
    {
        var excel = File.ReadAllBytes(ExcelFile("a.xlsx", "A"));
        var msg = MessageFile("email.msg", ("tasks.xlsx", excel), ("tasks.xlsx", excel));

        var found = ImportDrop.FindExcelFiles([msg], _temp.File("work"));

        Assert.Equal(2, found.ExcelFiles.Count);
        Assert.Equal(2, found.ExcelFiles.Distinct().Count());
    }

    [Fact]
    public void TheQuestionBeforeImporting_NamesTheFileAndListsTheTasks()
    {
        var rows = Enumerable.Range(1, 10).Select(i => new ImportedTaskRow { Title = $"Task {i}", Project = i == 1 ? "Website" : null, Priority = i == 1 ? "High" : null, DueDate = i == 1 ? new DateTime(2026, 10, 15) : null }).ToList();

        var text = ImportDrop.DescribeForConfirm([(@"C:\x\KanbanTask_Order.xlsx", rows)]);

        Assert.StartsWith("Import 10 tasks from KanbanTask_Order.xlsx?", text);
        Assert.Contains("• Task 1  (Website, High, due Oct 15)", text);
        Assert.Contains("• Task 8", text);
        Assert.DoesNotContain("• Task 9", text);
        Assert.Contains("• and 2 more", text);
        Assert.EndsWith("Each task can still be changed on the next screen.", text);

        var one = ImportDrop.DescribeForConfirm([("a.xlsx", [rows[1]]), ("b.xlsx", [rows[2]])]);
        Assert.StartsWith("Import 2 tasks from 2 files?", one);
    }
}
