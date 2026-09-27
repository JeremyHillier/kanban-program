using System.Windows.Documents;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Runs a saved report view straight from its stored settings - what Quick Report does - so the
// Report Builder screen doesn't have to be opened and filled in first. The Report Builder itself
// still builds from its own controls; the two agree because both call ReportService.BuildRows
// with the same values. Relative dates ("today+7") are resolved here, on the day the report runs.
public static class ReportRunner
{
    public static List<ReportRow> BuildRows(MainViewModel board, SavedReportView view, DateTime today)
    {
        var scope = Enum.TryParse<ReportArchiveScope>(view.ArchiveScope, out var parsed) ? parsed : ReportArchiveScope.BoardOnly;
        var unionFilters = board.CustomFilters
            .Where(f => f is not null && view.CustomFilterNames.Contains(f.Name))
            .Select(f => f!)
            .ToList();

        return ReportService.BuildRows(
            board.Columns,
            view.IncludedColumns.ToHashSet(),
            view.Project, view.Priority, view.Who, view.Goal, view.Flag, view.Due,
            RelativeDate.Resolve(view.DueFrom, today), RelativeDate.Resolve(view.DueTo, today), view.IncludeNoDueDate,
            unionFilters.Count > 0 ? unionFilters : null,
            view.SortLevel1, view.SortLevel2, view.SortLevel3,
            scope,
            scope == ReportArchiveScope.BoardOnly ? null : board.GetArchivedReportRows(),
            RelativeDate.Resolve(view.ArchivedFrom, today), RelativeDate.Resolve(view.ArchivedTo, today));
    }

    public static FixedDocument BuildDocument(MainViewModel board, SavedReportView view, DateTime today) =>
        ReportService.BuildFixedDocument(view.Title, BuildRows(board, view, today), view.GroupBy, view.IncludeNotes, view.IncludeSubTasks,
            view.IncludeSubTaskSummary, view.IsLandscape, Summarise(view, today));

    public static void SavePdf(MainViewModel board, SavedReportView view, DateTime today, string filePath) =>
        ReportService.SavePdf(view.Title, BuildRows(board, view, today), view.GroupBy, view.IncludeNotes, view.IncludeSubTasks, filePath,
            view.IncludeSubTaskSummary, view.IsLandscape, Summarise(view, today));

    // The line under the report's title saying what it covers. Written from the saved view, so it
    // matches what the Report Builder would print for the same choices, with the dates as they
    // were resolved today.
    internal static string Summarise(SavedReportView view, DateTime today)
    {
        var parts = new List<string>();

        if (view.CustomFilterNames.Count > 0) parts.Add($"Custom filters: {string.Join(", ", view.CustomFilterNames)}");
        else
        {
            var filters = new List<string>();
            if (view.Project.Count > 0) filters.Add($"Project: {string.Join(", ", view.Project)}");
            if (view.Priority.Count > 0) filters.Add($"Priority: {string.Join(", ", view.Priority)}");
            if (view.Who.Count > 0) filters.Add($"Who: {string.Join(", ", view.Who)}");
            if (view.Goal != "All") filters.Add($"Goal: {view.Goal}");
            if (view.Flag != "All") filters.Add($"Flag: {view.Flag}");
            if (view.Due != "All") filters.Add($"Due: {view.Due}");
            if (filters.Count > 0) parts.Add("Filters: " + string.Join(", ", filters));
        }

        var from = RelativeDate.Resolve(view.DueFrom, today);
        var to = RelativeDate.Resolve(view.DueTo, today);
        if (from is not null || to is not null || view.IncludeNoDueDate)
        {
            var noDue = view.IncludeNoDueDate ? " + tasks with no due date" : "";
            parts.Add($"Due date range: {from?.ToString("MMM d, yyyy") ?? "any"} to {to?.ToString("MMM d, yyyy") ?? "any"}{noDue}");
        }

        var sorts = new[] { view.SortLevel1, view.SortLevel2, view.SortLevel3 }.Where(s => s != "None").ToList();
        if (sorts.Count > 0) parts.Add("Sort order: " + string.Join(" then ", sorts));
        if (view.GroupBy != "None") parts.Add($"Grouped by: {(view.GroupBy == "Status" ? "Category" : view.GroupBy)}");

        if (view.ArchiveScope != nameof(ReportArchiveScope.BoardOnly))
        {
            var scopeText = view.ArchiveScope == nameof(ReportArchiveScope.ArchivedOnly) ? "Archived only" : "Board tasks + archived";
            var aFrom = RelativeDate.Resolve(view.ArchivedFrom, today);
            var aTo = RelativeDate.Resolve(view.ArchivedTo, today);
            if (aFrom is not null || aTo is not null) scopeText += $" (archived {aFrom?.ToString("MMM d, yyyy") ?? "any"} to {aTo?.ToString("MMM d, yyyy") ?? "any"})";
            parts.Add($"Scope: {scopeText}");
        }

        return parts.Count == 0 ? "No filters applied" : "Parameters: " + string.Join("   |   ", parts);
    }
}
