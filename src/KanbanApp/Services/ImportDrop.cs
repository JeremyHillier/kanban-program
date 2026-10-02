using System.IO;
using KanbanApp.Models;

namespace KanbanApp.Services;

// What to make of things dropped on the Import screen: Excel files taken as they are, and Excel
// attachments pulled out of any Outlook email (.msg) dropped whole. Everything else is left alone
// and named, so the user is told what was ignored rather than wondering why nothing happened.
public static class ImportDrop
{
    public sealed record Found(List<string> ExcelFiles, List<string> Ignored);

    public static bool IsExcelFile(string path) => Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    // workDir is where attachments taken out of emails are written.
    public static Found FindExcelFiles(IEnumerable<string> droppedPaths, string workDir)
    {
        var excel = new List<string>();
        var ignored = new List<string>();

        foreach (var path in droppedPaths)
        {
            if (IsExcelFile(path))
            {
                excel.Add(path);
            }
            else if (OutlookDragDropHelper.IsOutlookMessageFile(path))
            {
                var inside = OutlookDragDropHelper.ExtractMsgAttachments(path, workDir, IsExcelFile);
                if (inside.Count == 0) ignored.Add($"{Path.GetFileName(path)} (an email with no Excel file attached)");
                excel.AddRange(inside);
            }
            else
            {
                ignored.Add(Path.GetFileName(path));
            }
        }

        return new Found(excel, ignored);
    }

    // The question asked before anything is imported: which files, and the tasks in them. isUpdate
    // says which rows are a task already on the board, which the import will update rather than add.
    public static string DescribeForConfirm(IReadOnlyList<(string File, List<ImportedTaskRow> Rows)> files, Func<ImportedTaskRow, bool>? isUpdate = null)
    {
        const int titlesShown = 8;
        var rows = files.SelectMany(f => f.Rows).ToList();
        var lines = new List<string>
        {
            $"Import {rows.Count} task{(rows.Count == 1 ? "" : "s")} from {(files.Count == 1 ? Path.GetFileName(files[0].File) : $"{files.Count} files")}?",
            ""
        };
        lines.AddRange(rows.Take(titlesShown).Select(r => $"• {r.Title.Trim()}{Describe(r)}{(isUpdate?.Invoke(r) == true ? " - updates your copy" : "")}"));
        if (rows.Count > titlesShown) lines.Add($"• and {rows.Count - titlesShown} more");
        lines.Add("");
        var updates = isUpdate is null ? 0 : rows.Count(isUpdate);
        if (updates > 0)
        {
            lines.Add(updates == rows.Count
                ? $"{(updates == 1 ? "This is a task" : "These are tasks")} you already have, so {(updates == 1 ? "it" : "they")} will be updated rather than added again."
                : $"{updates} of them {(updates == 1 ? "is a task" : "are tasks")} you already have, so {(updates == 1 ? "it" : "they")} will be updated rather than added again.");
            lines.Add("");
        }
        lines.Add("Each task can still be changed on the next screen.");
        return string.Join("\n", lines);
    }

    private static string Describe(ImportedTaskRow row)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.Project)) parts.Add(row.Project.Trim());
        if (!string.IsNullOrWhiteSpace(row.Priority)) parts.Add(row.Priority.Trim());
        if (row.DueDate is { } due) parts.Add($"due {due:MMM d}");
        if (row.RecurrencePattern is { } pattern) parts.Add($"repeats {pattern.ToLowerInvariant()}");
        return parts.Count == 0 ? "" : $"  ({string.Join(", ", parts)})";
    }
}
