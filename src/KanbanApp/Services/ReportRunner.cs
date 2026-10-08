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
        view.IsStatistics
            ? ReportService.BuildStatisticsDocument(view.Title, BuildStatistics(board, view, today), SectionsOf(view), view.StatsBreakdown,
                view.StatsOverTime, view.IsLandscape, Summarise(view, today))
            : ReportService.BuildFixedDocument(view.Title, BuildRows(board, view, today), view.GroupBy, view.IncludeNotes, view.IncludeSubTasks,
                view.IncludeSubTaskSummary, view.IsLandscape, Summarise(view, today));

    public static void SavePdf(MainViewModel board, SavedReportView view, DateTime today, string filePath)
    {
        if (view.IsStatistics)
        {
            ReportService.SaveStatisticsPdf(view.Title, BuildStatistics(board, view, today), SectionsOf(view), view.StatsBreakdown,
                view.StatsOverTime, view.IsLandscape, Summarise(view, today), filePath);
            return;
        }

        ReportService.SavePdf(view.Title, BuildRows(board, view, today), view.GroupBy, view.IncludeNotes, view.IncludeSubTasks, filePath,
            view.IncludeSubTaskSummary, view.IsLandscape, Summarise(view, today));
    }

    // A statistics report covers every task its filters pick, on the board and archived, whatever
    // column it is in or when it is due - the period, not the due dates, decides what is counted.
    public static TaskStatisticsResult BuildStatistics(MainViewModel board, SavedReportView view, DateTime today)
    {
        var unionFilters = board.CustomFilters
            .Where(f => f is not null && view.CustomFilterNames.Contains(f.Name))
            .Select(f => f!)
            .ToList();

        var rows = ReportService.BuildRows(
            board.Columns,
            board.Columns.Select(c => c.Name).ToHashSet(),
            view.Project, view.Priority, view.Who, view.Goal, view.Flag, "All",
            null, null, false,
            unionFilters.Count > 0 ? unionFilters : null,
            "None", "None", "None",
            ReportArchiveScope.BoardAndArchived,
            board.GetArchivedReportRows());

        var history = board.GetStatisticsHistory();
        var (from, to) = PeriodOf(view, today, history);
        return TaskStatistics.Compute(rows, history, board.Columns.Select(c => (c.Name, c.DisplayName)).ToList(),
            new StatisticsOptions(from, to, view.StatsBreakdown, view.StatsOverTime), today);
    }

    // All time starts from the first task ever added.
    private static (DateTime From, DateTime To) PeriodOf(SavedReportView view, DateTime today, IReadOnlyDictionary<int, List<TaskEvent>>? history = null)
    {
        var earliest = history?.Values.SelectMany(e => e).Where(e => e.IsCreated).Select(e => (DateTime?)e.At).Min();
        return StatisticsPeriods.Resolve(view.StatsPeriod, today, RelativeDate.Resolve(view.StatsFrom, today), RelativeDate.Resolve(view.StatsTo, today), earliest);
    }

    private static ReportService.SavedStatisticsSections SectionsOf(SavedReportView view) =>
        new(view.StatsShowSummary, view.StatsShowBreakdown, view.StatsShowOverTime, view.StatsShowColumnTimes, view.StatsShowCharts);

    // The line under the report's title saying what it covers. Written from the saved view, so it
    // matches what the Report Builder would print for the same choices, with the dates as they
    // were resolved today.
    internal static string Summarise(SavedReportView view, DateTime today)
    {
        var parts = new List<string>();

        if (view.IsStatistics)
        {
            // All time's real start needs the task file; "all time" says it plainly enough.
            var (periodFrom, periodTo) = PeriodOf(view, today);
            parts.Add(view.StatsPeriod == StatisticsPeriods.AllTime
                ? "Period: all time"
                : $"Period: {StatisticsPeriods.Label(view.StatsPeriod)}, {StatisticsPeriods.Describe(periodFrom, periodTo)}");
        }

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

        if (view.IsStatistics)
        {
            if (view.StatsShowBreakdown && view.StatsBreakdown != "None") parts.Add($"By: {(view.StatsBreakdown == "Who" ? "Person" : view.StatsBreakdown)}");
            if (view.StatsShowOverTime) parts.Add(view.StatsOverTime == "Month" ? "Month by month" : "Week by week");
            return "Statistics   |   " + string.Join("   |   ", parts);
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
