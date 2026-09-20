using System.Globalization;
using ClosedXML.Excel;
using KanbanApp.Models;

namespace KanbanApp.Services;

public static class ImportService
{
    private static readonly string[] Headers = ["Title", "Category", "Priority", "Project", "Goal", "Due Date", "Who", "Start Date", "Waiting On"];
    private static readonly string[] Priorities = ["High", "Medium", "Normal", "Low"];

    public static void SaveTemplate(string filePath, IEnumerable<string> categories, IEnumerable<string> projects, IEnumerable<string> goals, IEnumerable<string> people)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Tasks");

        sheet.Range(1, 1, 1, Headers.Length).Merge();
        sheet.Cell(1, 1).Value = "One task per row below. Category and Priority must be chosen from their dropdown. "
            + "Project, Goal, and Who offer a dropdown of existing values, but you can type a new one instead. For more than one person, type the names in the Who cell with a semicolon between them (Sam Lee; Priya Patel) - the first is the lead. "
            + "Due Date and Start Date (optional, the earliest the task can be worked on): enter as MM/DD/YYYY (year optional, defaults to this year) — shown as DD-MMM-YYYY. Only Title is required.";
        sheet.Cell(1, 1).Style.Font.Italic = true;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(0x88, 0x88, 0x88);
        sheet.Cell(1, 1).Style.Alignment.WrapText = true;
        sheet.Row(1).Height = 45;

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
        sheet.Column(8).Width = 14;
        sheet.Column(9).Width = 24;

        const int maxDataRow = 500;
        sheet.Range(3, 6, maxDataRow, 6).Style.DateFormat.Format = "dd-mmm-yyyy";
        sheet.Range(3, 6, maxDataRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Range(3, 8, maxDataRow, 8).Style.DateFormat.Format = "dd-mmm-yyyy";
        sheet.Range(3, 8, maxDataRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        var listsSheet = workbook.AddWorksheet("ValidationLists");
        listsSheet.Visibility = XLWorksheetVisibility.VeryHidden;

        AddValidationList(sheet, listsSheet, column: 2, dataColumn: 2, maxDataRow, categories, restrict: true);
        AddValidationList(sheet, listsSheet, column: 3, dataColumn: 3, maxDataRow, Priorities, restrict: true);
        AddValidationList(sheet, listsSheet, column: 4, dataColumn: 4, maxDataRow, projects, restrict: false);
        AddValidationList(sheet, listsSheet, column: 5, dataColumn: 5, maxDataRow, goals, restrict: false);
        AddValidationList(sheet, listsSheet, column: 7, dataColumn: 7, maxDataRow, people, restrict: false);

        sheet.SheetView.FreezeRows(2);

        workbook.SaveAs(filePath);
    }

    private static void AddValidationList(IXLWorksheet sheet, IXLWorksheet listsSheet, int column, int dataColumn, int maxDataRow,
        IEnumerable<string> values, bool restrict)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        if (list.Count == 0) return;

        for (var i = 0; i < list.Count; i++)
        {
            listsSheet.Cell(i + 1, dataColumn).Value = list[i];
        }

        var listRange = listsSheet.Range(1, dataColumn, list.Count, dataColumn);
        var validation = sheet.Range(3, column, maxDataRow, column).CreateDataValidation();
        validation.List(listRange);

        if (!restrict)
        {
            // Still shows the dropdown for convenience, but a typed value that isn't on the list is accepted silently.
            validation.ShowErrorMessage = false;
            validation.ShowInputMessage = true;
            validation.InputTitle = "Existing or new";
            validation.InputMessage = "Pick from the list, or type a new value.";
        }
    }

    // Single-row version of the import template, used to attach one task to an email so the
    // recipient can pull it into their own board via the same Import Tasks feature - just the
    // headers ReadTasks looks for plus one data row, no instructions banner or dropdown validation
    // (the recipient's Category/Project/Goal/Who lists won't match the sender's anyway).
    public static void SaveSingleTaskFile(string filePath, ImportedTaskRow row)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Tasks");

        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE3, 0xE8, 0xEF);
        }

        sheet.Cell(2, 1).Value = row.Title;
        sheet.Cell(2, 2).Value = row.Category ?? string.Empty;
        sheet.Cell(2, 3).Value = row.Priority ?? string.Empty;
        sheet.Cell(2, 4).Value = row.Project ?? string.Empty;
        sheet.Cell(2, 5).Value = row.Goal ?? string.Empty;
        if (row.DueDate.HasValue)
        {
            sheet.Cell(2, 6).Value = row.DueDate.Value;
            sheet.Cell(2, 6).Style.DateFormat.Format = "dd-mmm-yyyy";
        }
        sheet.Cell(2, 7).Value = row.Who ?? string.Empty;
        if (row.StartDate.HasValue)
        {
            sheet.Cell(2, 8).Value = row.StartDate.Value;
            sheet.Cell(2, 8).Style.DateFormat.Format = "dd-mmm-yyyy";
        }
        sheet.Cell(2, 9).Value = row.WaitingOn ?? string.Empty;

        sheet.Columns().AdjustToContents();

        workbook.SaveAs(filePath);
    }

    // The headings each field answers to, so a customer's own spreadsheet imports without being
    // reshaped to match the template. Matched whole-cell, ignoring case and surrounding spaces.
    private static readonly string[] TitleHeadings = ["Title", "Task", "Task Details"];
    private static readonly string[] CategoryHeadings = ["Category", "Column", "Status"];
    private static readonly string[] PriorityHeadings = ["Priority"];
    private static readonly string[] ProjectHeadings = ["Project"];
    private static readonly string[] GoalHeadings = ["Goal"];
    private static readonly string[] DueDateHeadings = ["Due Date", "Due"];
    private static readonly string[] WhoHeadings = ["Who", "Assigned To", "Assignee"];
    private static readonly string[] StartDateHeadings = ["Start Date", "Start", "Not Before"];
    private static readonly string[] WaitingOnHeadings = ["Waiting On", "Waiting For", "Blocked By"];

    private static readonly string[][] OtherHeadings =
        [CategoryHeadings, PriorityHeadings, ProjectHeadings, GoalHeadings, DueDateHeadings, WhoHeadings, StartDateHeadings, WaitingOnHeadings];

    private static bool IsHeading(IXLCell cell, string[] headings) =>
        headings.Contains(cell.GetString().Trim(), StringComparer.OrdinalIgnoreCase);

    // The header row is wherever the title heading is - any column, any row, under any banner or
    // blank rows. A row that has a title heading AND at least one other known heading wins, so a
    // stray cell that just says "Task" above the real headings (a sheet title, say) isn't mistaken
    // for them. If no row has two, the first row with a title heading alone is used, which is what a
    // plain one-column list of tasks looks like.
    private static IXLRow? FindHeaderRow(IXLWorksheet sheet)
    {
        IXLRow? titleOnly = null;
        foreach (var row in sheet.RowsUsed())
        {
            var cells = row.CellsUsed().ToList();
            if (!cells.Any(c => IsHeading(c, TitleHeadings))) continue;
            if (cells.Any(c => OtherHeadings.Any(h => IsHeading(c, h)))) return row;
            titleOnly ??= row;
        }
        return titleOnly;
    }

    public static List<ImportedTaskRow> ReadTasks(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var headerRow = FindHeaderRow(sheet);
        if (headerRow is null) return [];

        // First column wins if a heading appears twice.
        var columnIndexByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header)) columnIndexByHeader.TryAdd(header, cell.Address.ColumnNumber);
        }

        int? ColumnFor(string[] names)
        {
            foreach (var name in names)
            {
                if (columnIndexByHeader.TryGetValue(name, out var index)) return index;
            }
            return null;
        }

        var titleCol = ColumnFor(TitleHeadings);
        if (titleCol is null) return [];
        var categoryCol = ColumnFor(CategoryHeadings);
        var priorityCol = ColumnFor(PriorityHeadings);
        var projectCol = ColumnFor(ProjectHeadings);
        var goalCol = ColumnFor(GoalHeadings);
        var dueDateCol = ColumnFor(DueDateHeadings);
        var whoCol = ColumnFor(WhoHeadings);
        var startDateCol = ColumnFor(StartDateHeadings);
        var waitingOnCol = ColumnFor(WaitingOnHeadings);

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
                else if (DateTime.TryParse(dueCell.GetString().Trim(), CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var parsedText))
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
                StartDate = startDateCol is not null ? ReadDate(row.Cell(startDateCol.Value)) : null,
                WaitingOn = waitingOnCol is not null ? row.Cell(waitingOnCol.Value).GetString().Trim() : null,
                Who = whoCol is not null ? row.Cell(whoCol.Value).GetString().Trim() : null
            });
        }

        return results;
    }

    // A real Excel date, or text that reads as one (US order, as the template shows it).
    private static DateTime? ReadDate(IXLCell cell)
    {
        if (cell.TryGetValue(out DateTime parsedDate)) return parsedDate;
        return DateTime.TryParse(cell.GetString().Trim(), CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var parsedText) ? parsedText : null;
    }
}
