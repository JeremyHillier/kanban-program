using ClosedXML.Excel;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Tests;

public sealed class ImportServiceTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SingleTaskFile_ReadsBackEveryField()
    {
        var path = _temp.File("one.xlsx");
        ImportService.SaveSingleTaskFile(path, new ImportedTaskRow
        {
            Title = "Renew insurance \"home & auto\"",
            Category = "In Progress",
            Priority = "High",
            Project = "Household",
            Goal = "Stay organised",
            DueDate = new DateTime(2026, 12, 25),
            StartDate = new DateTime(2026, 12, 1),
            Who = "Jane Doe"
        });

        var row = Assert.Single(ImportService.ReadTasks(path));
        Assert.Equal("Renew insurance \"home & auto\"", row.Title);
        Assert.Equal("In Progress", row.Category);
        Assert.Equal("High", row.Priority);
        Assert.Equal("Household", row.Project);
        Assert.Equal("Stay organised", row.Goal);
        Assert.Equal(new DateTime(2026, 12, 25), row.DueDate);
        Assert.Equal(new DateTime(2026, 12, 1), row.StartDate);
        Assert.Equal("Jane Doe", row.Who);
    }

    [Fact]
    public void TemplateLayout_SkipsTheInstructionsRowAndBlankRows()
    {
        var path = _temp.File("template.xlsx");
        ImportService.SaveTemplate(path, ["To Do"], ["General"], [], []);
        using (var workbook = new XLWorkbook(path))
        {
            var sheet = workbook.Worksheet("Tasks");
            sheet.Cell(3, 1).Value = "First";
            sheet.Cell(5, 1).Value = "Third, after a blank row";
            workbook.Save();
        }

        Assert.Equal(["First", "Third, after a blank row"], ImportService.ReadTasks(path).Select(r => r.Title));
    }

    [Fact]
    public void ReorderedColumnsAndAlternativeHeaders_StillMapCorrectly()
    {
        var path = _temp.File("custom.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "Title";
            sheet.Cell(1, 2).Value = "Assigned To";
            sheet.Cell(1, 3).Value = "Due";
            sheet.Cell(1, 4).Value = "Status";
            sheet.Cell(2, 1).Value = "Book venue";
            sheet.Cell(2, 2).Value = "Sam";
            sheet.Cell(2, 3).Value = "03/04/2026"; // typed as text, read as month/day
            sheet.Cell(2, 4).Value = "Waiting";
            workbook.SaveAs(path);
        }

        var row = Assert.Single(ImportService.ReadTasks(path));
        Assert.Equal("Book venue", row.Title);
        Assert.Equal("Sam", row.Who);
        Assert.Equal(new DateTime(2026, 3, 4), row.DueDate);
        Assert.Equal("Waiting", row.Category);
        Assert.Null(row.Priority);
    }

    [Fact]
    public void SheetWithoutATitleHeader_ImportsNothing()
    {
        var path = _temp.File("wrong.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "Name";
            sheet.Cell(2, 1).Value = "Something";
            workbook.SaveAs(path);
        }

        Assert.Empty(ImportService.ReadTasks(path));
    }

    private string Workbook(string name, Action<IXLWorksheet> fill)
    {
        var path = _temp.File(name);
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Sheet1"));
        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public void TaskHeadingInAnotherColumn_UnderATitleBannerAndBlankRow_Imports()
    {
        var path = Workbook("own.xlsx", sheet =>
        {
            sheet.Cell(1, 1).Value = "Office move - action list";
            sheet.Cell(3, 1).Value = "Owner";     // no recognised heading in column A
            sheet.Cell(3, 2).Value = "  task ";   // heading in column B, odd case and spacing
            sheet.Cell(3, 3).Value = "Priority";
            sheet.Cell(4, 1).Value = "Pat";
            sheet.Cell(4, 2).Value = "Book movers";
            sheet.Cell(4, 3).Value = "High";
            sheet.Cell(5, 2).Value = "Order boxes";
        });

        var rows = ImportService.ReadTasks(path);

        Assert.Equal(["Book movers", "Order boxes"], rows.Select(r => r.Title));
        Assert.Equal("High", rows[0].Priority);
        Assert.Null(rows[0].Who); // "Owner" isn't a heading the import knows
    }

    [Fact]
    public void AStrayTaskCellAboveTheRealHeadings_IsNotMistakenForThem()
    {
        var path = Workbook("stray.xlsx", sheet =>
        {
            sheet.Cell(1, 1).Value = "Task";      // a sheet title, not a heading row
            sheet.Cell(2, 1).Value = "Prepared by Sam";
            sheet.Cell(4, 1).Value = "Status";
            sheet.Cell(4, 2).Value = "Task Details";
            sheet.Cell(5, 1).Value = "Waiting";
            sheet.Cell(5, 2).Value = "Chase invoice";
        });

        var row = Assert.Single(ImportService.ReadTasks(path));
        Assert.Equal("Chase invoice", row.Title);
        Assert.Equal("Waiting", row.Category);
    }

    [Fact]
    public void APlainOneColumnListHeadedTask_Imports()
    {
        var path = Workbook("list.xlsx", sheet =>
        {
            sheet.Cell(1, 1).Value = "Task";
            sheet.Cell(2, 1).Value = "First";
            sheet.Cell(3, 1).Value = "Second";
        });

        Assert.Equal(["First", "Second"], ImportService.ReadTasks(path).Select(r => r.Title));
    }

    [Fact]
    public void ATaskWhoseTitleIsTheWordTask_IsDataNotAHeading()
    {
        var path = Workbook("word.xlsx", sheet =>
        {
            sheet.Cell(1, 1).Value = "Title";
            sheet.Cell(1, 2).Value = "Priority";
            sheet.Cell(2, 1).Value = "Task";
            sheet.Cell(2, 2).Value = "Low";
            sheet.Cell(3, 1).Value = "Another";
        });

        Assert.Equal(["Task", "Another"], ImportService.ReadTasks(path).Select(r => r.Title));
    }
}
