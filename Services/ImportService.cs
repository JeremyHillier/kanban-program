using ClosedXML.Excel;
using KanbanApp.Models;

namespace KanbanApp.Services;

public static class ImportService
{
    private static readonly string[] Headers = ["Title", "Category", "Priority", "Project", "Goal", "Due Date", "Who"];
    private static readonly string[] Priorities = ["High", "Medium", "Normal"];

    public static void SaveTemplate(string filePath, IEnumerable<string> categories, IEnumerable<string> projects, IEnumerable<string> goals)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Tasks");

        sheet.Range(1, 1, 1, Headers.Length).Merge();
        sheet.Cell(1, 1).Value = "One task per row below. Category, Priority, Project, and Goal offer a dropdown of valid values. "
            + "Due Date format: DD/MM/YYYY. Only Title is required.";
        sheet.Cell(1, 1).Style.Font.Italic = true;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(0x88, 0x88, 0x88);
        sheet.Cell(1, 1).Style.Alignment.WrapText = true;
        sheet.Row(1).Height = 30;

        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE3, 0xE8, 0xEF);
        }

        sheet.Column(1).Width = 40;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 20;
        sheet.Column(5).Width = 20;
        sheet.Column(6).Width = 14;
        sheet.Column(7).Width = 14;

        const int maxDataRow = 500;
        sheet.Range(3, 6, maxDataRow, 6).Style.DateFormat.Format = "dd/mm/yyyy";

        var listsSheet = workbook.AddWorksheet("ValidationLists");
        listsSheet.Visibility = XLWorksheetVisibility.VeryHidden;

        AddValidationList(sheet, listsSheet, column: 1, dataColumn: 2, maxDataRow, categories);
        AddValidationList(sheet, listsSheet, column: 2, dataColumn: 3, maxDataRow, Priorities);
        AddValidationList(sheet, listsSheet, column: 3, dataColumn: 4, maxDataRow, projects);
        AddValidationList(sheet, listsSheet, column: 4, dataColumn: 5, maxDataRow, goals);

        sheet.SheetView.FreezeRows(2);

        workbook.SaveAs(filePath);
    }

    private static void AddValidationList(IXLWorksheet sheet, IXLWorksheet listsSheet, int column, int dataColumn, int maxDataRow, IEnumerable<string> values)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        if (list.Count == 0) return;

        for (var i = 0; i < list.Count; i++)
        {
            listsSheet.Cell(i + 1, dataColumn).Value = list[i];
        }

        var listRange = listsSheet.Range(1, dataColumn, list.Count, dataColumn);
        sheet.Range(3, column, maxDataRow, column).CreateDataValidation().List(listRange);
    }

    public static List<ImportedTaskRow> ReadTasks(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var headerRow = sheet.RowsUsed()
            .FirstOrDefault(r => string.Equals(r.Cell(1).GetString().Trim(), "Title", StringComparison.OrdinalIgnoreCase));
        if (headerRow is null) return [];

        var columnIndexByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header)) columnIndexByHeader[header] = cell.Address.ColumnNumber;
        }

        int? ColumnFor(params string[] names)
        {
            foreach (var name in names)
            {
                if (columnIndexByHeader.TryGetValue(name, out var index)) return index;
            }
            return null;
        }

        var titleCol = ColumnFor("Title", "Task", "Task Details");
        if (titleCol is null) return [];
        var categoryCol = ColumnFor("Category", "Column", "Status");
        var priorityCol = ColumnFor("Priority");
        var projectCol = ColumnFor("Project");
        var goalCol = ColumnFor("Goal");
        var dueDateCol = ColumnFor("Due Date", "Due");
        var whoCol = ColumnFor("Who", "Assigned To", "Assignee");

        var results = new List<ImportedTaskRow>();
        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var title = row.Cell(titleCol.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            DateTime? dueDate = null;
            if (dueDateCol is not null)
            {
                var dueCell = row.Cell(dueDateCol.Value);
                if (dueCell.TryGetValue(out DateTime parsedDate))
                {
                    dueDate = parsedDate;
                }
                else if (DateTime.TryParse(dueCell.GetString().Trim(), out var parsedText))
                {
                    dueDate = parsedText;
                }
            }

            results.Add(new ImportedTaskRow
            {
                Title = title,
                Category = categoryCol is not null ? row.Cell(categoryCol.Value).GetString().Trim() : null,
                Priority = priorityCol is not null ? row.Cell(priorityCol.Value).GetString().Trim() : null,
                Project = projectCol is not null ? row.Cell(projectCol.Value).GetString().Trim() : null,
                Goal = goalCol is not null ? row.Cell(goalCol.Value).GetString().Trim() : null,
                DueDate = dueDate,
                Who = whoCol is not null ? row.Cell(whoCol.Value).GetString().Trim() : null
            });
        }

        return results;
    }
}
