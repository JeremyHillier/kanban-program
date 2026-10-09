using System.Globalization;
using ClosedXML.Excel;
using KanbanApp.Models;

namespace KanbanApp.Services;

public static class ImportService
{
    // The template's and the emailed task file's columns, in order. Reading goes by heading, not
    // position, so files made before a column was added still import, and older copies of the app
    // reading a newer file simply ignore the columns they don't know.
    private static readonly string[] Headers =
        ["Title", "Category", "Priority", "Project", "Goal", "Due Date", "Who", "Start Date", "Waiting On",
         "Due Time", "Repeats", "Repeat Times", "Flags", "Website", "Notes", "Sub-tasks", "Task ID"];

    private static int Col(string header) => Array.IndexOf(Headers, header) + 1;

    // priorities is the task file's own list; left out, the standard four.
    public static void SaveTemplate(string filePath, IEnumerable<string> categories, IEnumerable<string> projects, IEnumerable<string> goals, IEnumerable<string> people,
        IEnumerable<string>? priorities = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Tasks");

        sheet.Range(1, 1, 1, Headers.Length).Merge();
        sheet.Cell(1, 1).Value = "One task per row below. Category, Priority and Repeats must be chosen from their dropdown. "
            + "Project, Goal, and Who offer a dropdown of existing values, but you can type a new one instead. For more than one person, type the names in the Who cell with a semicolon between them (Sam Lee; Priya Patel) - the first is the lead. Flags work the same way. "
            + "Due Date and Start Date (optional, the earliest the task can be worked on): enter as MM/DD/YYYY (year optional, defaults to this year) — shown as DD-MMM-YYYY. Due Time like 2:30 PM. "
            + "Repeat Times: how many times the task happens in all, counting this one (blank keeps repeating). Sub-tasks: one per line, starting [x] for one already done. Task ID is filled in by the app - leave it blank. Only Title is required.";
        sheet.Cell(1, 1).Style.Font.Italic = true;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(0x88, 0x88, 0x88);
        sheet.Cell(1, 1).Style.Alignment.WrapText = true;
        sheet.Row(1).Height = 60;

        WriteHeaderRow(sheet, row: 2);

        var widths = new Dictionary<string, double>
        {
            ["Title"] = 40, ["Category"] = 16, ["Priority"] = 12, ["Project"] = 20, ["Goal"] = 20, ["Due Date"] = 14,
            ["Who"] = 14, ["Start Date"] = 14, ["Waiting On"] = 24, ["Due Time"] = 11, ["Repeats"] = 14, ["Repeat Times"] = 13,
            ["Flags"] = 18, ["Website"] = 30, ["Notes"] = 40, ["Sub-tasks"] = 34, ["Task ID"] = 12
        };
        foreach (var (header, width) in widths) sheet.Column(Col(header)).Width = width;

        const int maxDataRow = 500;
        foreach (var dateHeader in new[] { "Due Date", "Start Date" })
        {
            sheet.Range(3, Col(dateHeader), maxDataRow, Col(dateHeader)).Style.DateFormat.Format = "dd-mmm-yyyy";
            sheet.Range(3, Col(dateHeader), maxDataRow, Col(dateHeader)).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }
        // Text, so "2:30 PM" stays as typed rather than becoming an Excel time - both read back the same.
        sheet.Range(3, Col("Due Time"), maxDataRow, Col("Due Time")).Style.NumberFormat.Format = "@";
        foreach (var wrapped in new[] { "Notes", "Sub-tasks" })
        {
            sheet.Range(3, Col(wrapped), maxDataRow, Col(wrapped)).Style.Alignment.WrapText = true;
        }

        var listsSheet = workbook.AddWorksheet("ValidationLists");
        listsSheet.Visibility = XLWorksheetVisibility.VeryHidden;

        AddValidationList(sheet, listsSheet, column: Col("Category"), dataColumn: 1, maxDataRow, categories, restrict: true);
        AddValidationList(sheet, listsSheet, column: Col("Priority"), dataColumn: 2, maxDataRow, priorities ?? ViewModels.PriorityList.Fallback.Names, restrict: true);
        AddValidationList(sheet, listsSheet, column: Col("Project"), dataColumn: 3, maxDataRow, projects, restrict: false);
        AddValidationList(sheet, listsSheet, column: Col("Goal"), dataColumn: 4, maxDataRow, goals, restrict: false);
        AddValidationList(sheet, listsSheet, column: Col("Who"), dataColumn: 5, maxDataRow, people, restrict: false);
        AddValidationList(sheet, listsSheet, column: Col("Repeats"), dataColumn: 6, maxDataRow, RecurrencePatterns.All, restrict: true);

        sheet.SheetView.FreezeRows(2);

        workbook.SaveAs(filePath);
    }

    private static void WriteHeaderRow(IXLWorksheet sheet, int row)
    {
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE3, 0xE8, 0xEF);
        }
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

    // The import template without its instructions banner or drop-down lists, holding real tasks: what
    // goes with an emailed task (or a group of them) so the recipient can pull them into their own
    // board through Import Tasks. Just the headings ReadTasks looks for and a row per task (the
    // recipient's Category/Project/Goal/Who lists won't match the sender's anyway).
    public static void SaveSingleTaskFile(string filePath, ImportedTaskRow row) => SaveTasksFile(filePath, [row]);

    public static void SaveTasksFile(string filePath, IReadOnlyList<ImportedTaskRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Tasks");
        WriteHeaderRow(sheet, row: 1);
        for (var i = 0; i < rows.Count; i++) WriteTaskRow(sheet, i + 2, rows[i]);

        sheet.Columns().AdjustToContents();
        foreach (var wrapped in new[] { "Notes", "Sub-tasks" })
        {
            // A long note would otherwise make one column as wide as the whole note.
            if (sheet.Column(Col(wrapped)).Width > 50) sheet.Column(Col(wrapped)).Width = 50;
            for (var i = 0; i < rows.Count; i++) sheet.Cell(i + 2, Col(wrapped)).Style.Alignment.WrapText = true;
        }

        workbook.SaveAs(filePath);
    }

    private static void WriteTaskRow(IXLWorksheet sheet, int line, ImportedTaskRow row)
    {
        void Text(string header, string? value) => sheet.Cell(line, Col(header)).Value = value ?? string.Empty;
        void Date(string header, DateTime? value)
        {
            if (value is null) return;
            sheet.Cell(line, Col(header)).Value = value.Value;
            sheet.Cell(line, Col(header)).Style.DateFormat.Format = "dd-mmm-yyyy";
        }

        Text("Title", row.Title);
        Text("Category", row.Category);
        Text("Priority", row.Priority);
        Text("Project", row.Project);
        Text("Goal", row.Goal);
        Date("Due Date", row.DueDate);
        Text("Who", row.Who);
        Date("Start Date", row.StartDate);
        Text("Waiting On", row.WaitingOn);
        Text("Due Time", FormatTime(row.DueTime));
        Text("Repeats", row.RecurrencePattern);
        if (row.RecurrencePattern is not null && row.RecurrenceCount is { } count) sheet.Cell(line, Col("Repeat Times")).Value = count;
        Text("Flags", row.Flags);
        Text("Website", row.WebsiteUrl);
        Text("Notes", row.Notes);
        Text("Sub-tasks", FormatSubTasks(row.SubTasks));
        Text("Task ID", row.ShareId);
    }

    // "14:30" -> "2:30 PM", the same in every language Windows runs in, so any copy reads it back.
    private static string FormatTime(string? stored) =>
        TimeSpan.TryParse(stored, CultureInfo.InvariantCulture, out var time) ? DateTime.Today.Add(time).ToString("h:mm tt", CultureInfo.InvariantCulture) : string.Empty;

    // One sub-task per line; a done one starts "[x] ".
    internal static string FormatSubTasks(IEnumerable<(string Title, bool IsDone)> subTasks) =>
        string.Join("\n", subTasks.Where(s => !string.IsNullOrWhiteSpace(s.Title)).Select(s => (s.IsDone ? "[x] " : "") + s.Title.Trim()));

    // The reverse, forgiving of how a person would type it: blank lines skipped, "[ ]", "[x]", "[X]"
    // or "[✓]" at the start of a line, and "- " or "• " bullets.
    internal static List<(string Title, bool IsDone)> ParseSubTasks(string? cell)
    {
        var result = new List<(string, bool)>();
        foreach (var raw in (cell ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("- ") || line.StartsWith("• ")) line = line[2..].TrimStart();

            var done = false;
            if (line.Length >= 3 && line[0] == '[' && line[2] == ']')
            {
                done = line[1] is 'x' or 'X' or '✓' or '✔';
                line = line[3..].TrimStart();
            }
            if (line.Length > 0) result.Add((line, done));
        }
        return result;
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
    private static readonly string[] DueTimeHeadings = ["Due Time", "Time"];
    private static readonly string[] RepeatsHeadings = ["Repeats", "Recurring", "Recurrence", "Repeat"];
    private static readonly string[] RepeatTimesHeadings = ["Repeat Times", "Times", "Occurrences"];
    private static readonly string[] FlagsHeadings = ["Flags", "Flag", "Tags"];
    private static readonly string[] WebsiteHeadings = ["Website", "Link", "URL"];
    private static readonly string[] NotesHeadings = ["Notes", "Note", "Description"];
    private static readonly string[] SubTasksHeadings = ["Sub-tasks", "Subtasks", "Sub Tasks", "Checklist"];
    private static readonly string[] TaskIdHeadings = ["Task ID"];

    private static readonly string[][] OtherHeadings =
        [CategoryHeadings, PriorityHeadings, ProjectHeadings, GoalHeadings, DueDateHeadings, WhoHeadings, StartDateHeadings, WaitingOnHeadings,
         DueTimeHeadings, RepeatsHeadings, RepeatTimesHeadings, FlagsHeadings, WebsiteHeadings, NotesHeadings, SubTasksHeadings, TaskIdHeadings];

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
        var dueTimeCol = ColumnFor(DueTimeHeadings);
        var repeatsCol = ColumnFor(RepeatsHeadings);
        var repeatTimesCol = ColumnFor(RepeatTimesHeadings);
        var flagsCol = ColumnFor(FlagsHeadings);
        var websiteCol = ColumnFor(WebsiteHeadings);
        var notesCol = ColumnFor(NotesHeadings);
        var subTasksCol = ColumnFor(SubTasksHeadings);
        var taskIdCol = ColumnFor(TaskIdHeadings);

        var results = new List<ImportedTaskRow>();
        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var title = row.Cell(titleCol.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            string? Text(int? col) => col is null ? null : row.Cell(col.Value).GetString().Trim();
            string? TextOrNull(int? col) => Text(col) is { Length: > 0 } text ? text : null;

            var pattern = RecurrencePatterns.Find(Text(repeatsCol));
            results.Add(new ImportedTaskRow
            {
                Title = title,
                Category = Text(categoryCol),
                Priority = Text(priorityCol),
                Project = Text(projectCol),
                Goal = Text(goalCol),
                DueDate = dueDateCol is not null ? ReadDate(row.Cell(dueDateCol.Value)) : null,
                DueTime = dueTimeCol is not null ? ReadTime(row.Cell(dueTimeCol.Value)) : null,
                StartDate = startDateCol is not null ? ReadDate(row.Cell(startDateCol.Value)) : null,
                WaitingOn = Text(waitingOnCol),
                Who = Text(whoCol),
                RecurrencePattern = pattern,
                RecurrenceCount = pattern is not null && repeatTimesCol is not null ? ReadCount(row.Cell(repeatTimesCol.Value)) : null,
                Flags = TextOrNull(flagsCol),
                WebsiteUrl = TextOrNull(websiteCol),
                Notes = notesCol is null ? null : row.Cell(notesCol.Value).GetString() is { } notes && notes.Trim().Length > 0 ? notes.Trim() : null,
                SubTasks = subTasksCol is null ? [] : ParseSubTasks(row.Cell(subTasksCol.Value).GetString()),
                ShareId = NormalizeShareId(Text(taskIdCol))
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

    // As "HH:mm". Text like "2:30 PM" or "14:30", or a time Excel has turned into one of its own.
    // A cell holding a whole date (midnight) is not a time.
    private static string? ReadTime(IXLCell cell)
    {
        if (cell.DataType == XLDataType.TimeSpan && cell.TryGetValue(out TimeSpan span)) return $"{span.Hours:00}:{span.Minutes:00}";
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue(out DateTime when) && when.TimeOfDay != TimeSpan.Zero)
        {
            return $"{when.Hour:00}:{when.Minute:00}";
        }
        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double fraction) && fraction is > 0 and < 1)
        {
            var minutes = (int)Math.Round(fraction * 24 * 60);
            return $"{minutes / 60 % 24:00}:{minutes % 60:00}";
        }
        return DueTimeParser.Parse(cell.GetString(), preferPm: null);
    }

    // A task ID the app wrote (a GUID), in one standard spelling; anything else is ignored, so a
    // typed-in value can never point the import at the wrong task.
    internal static string? NormalizeShareId(string? text) =>
        Guid.TryParse(text?.Trim(), out var id) && id != Guid.Empty ? id.ToString("N") : null;

    // A whole number of 1 or more; anything else means "no end".
    private static int? ReadCount(IXLCell cell)
    {
        if (cell.TryGetValue(out double number) && number >= 1 && number == Math.Floor(number)) return (int)Math.Min(number, 999);
        return int.TryParse(cell.GetString().Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1 ? Math.Min(parsed, 999) : null;
    }
}
