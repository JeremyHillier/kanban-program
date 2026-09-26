using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Report rows: which tasks a report takes, in what order and groups, and the lines that describe each one.
// Drawing them is in ReportService.Preview (on screen and printed) and ReportService.Pdf.
public static partial class ReportService
{
    // unionFilters, when non-empty, REPLACES the six discrete filter params entirely for matching
    // purposes: a card is included if it matches ANY one of the selected saved custom filters (an
    // OR/union across filters, not an AND on top of the dropdowns above) - each captured slot's own
    // Due/DueFrom/DueTo is used as-is, independent of dueRangeFrom/dueRangeTo/includeNoDueDate below
    // (those are specific to this report's own due-date-range fields, not to the saved filters).
    public static List<ReportRow> BuildRows(
        IEnumerable<ColumnViewModel> columns,
        HashSet<string> includeColumns,
        List<string> projectFilter, List<string> priorityFilter, List<string> whoFilter, string goalFilter, string flagFilter, string dueFilter,
        DateTime? dueRangeFrom, DateTime? dueRangeTo, bool includeNoDueDate,
        List<CustomFilter>? unionFilters,
        string sortLevel1, string sortLevel2, string sortLevel3,
        ReportArchiveScope archiveScope = ReportArchiveScope.BoardOnly,
        IEnumerable<(CardViewModel Card, string ColumnName)>? archivedCards = null,
        DateTime? archivedFrom = null, DateTime? archivedTo = null)
    {
        bool CardMatches(CardViewModel card) => unionFilters is { Count: > 0 }
            ? unionFilters.Any(f => Matches(card, f))
            : Matches(card, projectFilter, priorityFilter, whoFilter, goalFilter, flagFilter, dueFilter, dueRangeFrom, dueRangeTo, includeNoDueDate);

        var columnList = columns.ToList();
        var categoryOrder = columnList.Select(c => c.DisplayName).ToList();

        var rows = new List<ReportRow>();

        if (archiveScope != ReportArchiveScope.ArchivedOnly)
        {
            foreach (var column in columnList)
            {
                if (!includeColumns.Contains(column.Name)) continue;

                foreach (var card in column.Cards)
                {
                    if (!CardMatches(card)) continue;

                    rows.Add(BuildRow(card, column.DisplayName, isArchived: false));
                }
            }
        }

        if (archiveScope != ReportArchiveScope.BoardOnly && archivedCards is not null)
        {
            foreach (var (card, columnName) in archivedCards)
            {
                if (!CardMatches(card)) continue;
                if (archivedFrom is not null && (card.ArchivedAt is null || card.ArchivedAt.Value.Date < archivedFrom.Value.Date)) continue;
                if (archivedTo is not null && (card.ArchivedAt is null || card.ArchivedAt.Value.Date > archivedTo.Value.Date)) continue;

                rows.Add(BuildRow(card, columnName, isArchived: true));
            }
        }

        // Sorted here (not in BuildFixedDocument/SavePdf) so both rendering paths get the same order
        // for free: LINQ's GroupBy preserves input order within each group, so pre-sorting the flat
        // list before it's grouped downstream naturally orders rows within whatever group they land in.
        return SortRows(rows, sortLevel1, sortLevel2, sortLevel3, categoryOrder);
    }

    public static List<SubTaskSummaryRow> BuildSubTaskSummary(List<ReportRow> rows) =>
        rows.SelectMany(r => r.SubTasks.Select(st => (ParentTitle: r.Title, SubTaskTitle: st.Title, st.IsDone)))
            .GroupBy(x => (x.ParentTitle, x.SubTaskTitle))
            .Select(g => new SubTaskSummaryRow
            {
                ParentTitle = g.Key.ParentTitle,
                SubTaskTitle = g.Key.SubTaskTitle,
                CompletedCount = g.Count(x => x.IsDone),
                TotalCount = g.Count()
            })
            .OrderBy(s => s.ParentTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SubTaskTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static ReportRow BuildRow(CardViewModel card, string columnName, bool isArchived) => new()
    {
        Title = card.Title,
        ColumnName = columnName,
        ProjectName = card.ProjectName,
        Priority = card.Priority,
        DueDate = card.DueDate,
        StartDate = card.StartDate,
        WaitingOn = card.WaitingOn,
        Who = card.WhoId is null ? null : card.WhoName,
        People = card.People.Select(p => p.Name).ToList(),
        GoalName = card.GoalName,
        Flags = card.Flags.Select(f => f.Name).ToList(),
        SubTasks = card.SubTasks.Select(s => (s.Title, s.IsDone)).ToList(),
        Notes = card.Notes,
        ArchivedAt = card.ArchivedAt,
        CompletedAt = card.CompletedAt,
        IsArchived = isArchived
    };

    private static bool Matches(CardViewModel card, List<string> projectFilter, List<string> priorityFilter, List<string> whoFilter,
        string goalFilter, string flagFilter, string dueFilter, DateTime? dueRangeFrom = null, DateTime? dueRangeTo = null, bool includeNoDueDate = false)
    {
        if (projectFilter.Count > 0 && !projectFilter.Contains(card.ProjectName)) return false;
        if (priorityFilter.Count > 0 && !priorityFilter.Contains(card.Priority)) return false;

        if (whoFilter.Count > 0)
        {
            // A shared task counts for each of its people.
            var matchesWho = card.People.Count == 0 ? whoFilter.Contains("Unassigned") : card.People.Any(p => whoFilter.Contains(p.Name));
            if (!matchesWho) return false;
        }

        if (goalFilter == "Unassigned")
        {
            if (card.GoalId is not null) return false;
        }
        else if (goalFilter != "All" && card.GoalName != goalFilter) return false;

        if (flagFilter == "Unassigned")
        {
            if (card.Flags.Count > 0) return false;
        }
        else if (flagFilter != "All" && card.Flags.All(f => f.Name != flagFilter)) return false;

        // A due-date range (set on the report itself, not part of a saved custom filter) takes
        // priority over the preset Due bucket when both happen to be present.
        if (dueRangeFrom is not null || dueRangeTo is not null)
        {
            var inRange = card.DueDate is not null
                && (dueRangeFrom is null || card.DueDate.Value.Date >= dueRangeFrom.Value.Date)
                && (dueRangeTo is null || card.DueDate.Value.Date <= dueRangeTo.Value.Date);
            var noDueDateOk = includeNoDueDate && card.DueDate is null;
            if (!inRange && !noDueDateOk) return false;
        }
        else if (dueFilter != "All")
        {
            var today = DateTime.Today;
            var matchesDue = dueFilter switch
            {
                "Today" => (card.DueDate is not null && card.DueDate.Value.Date <= today) || MainViewModel.StartsBetween(card, today, today),
                "Tomorrow" => card.DueDate?.Date == today.AddDays(1) || MainViewModel.StartsBetween(card, today.AddDays(1), today.AddDays(1)),
                "Within a Week" => (card.DueDate is not null && card.DueDate.Value.Date >= today && card.DueDate.Value.Date <= today.AddDays(7)) || MainViewModel.StartsBetween(card, today, today.AddDays(7)),
                "No Due Date" => card.DueDate is null,
                _ => true
            };
            if (!matchesDue) return false;
        }

        return true;
    }

    private static bool Matches(CardViewModel card, CustomFilter filter) =>
        Matches(card, filter.Project, filter.Priority, filter.Who, filter.Goal, filter.Flag, filter.Due,
            ParseDate(filter.DueFrom), ParseDate(filter.DueTo));

    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value, out var parsed) ? parsed : null;


    // Ranks by the board's own column order (categoryOrder, taken from the Columns passed into
    // BuildRows - already in SortOrder) rather than alphabetically, so the default To Do/In
    // Progress/On Hold/Waiting/Done ordering falls out for free. Archived rows carry whatever
    // column they were archived from, but are always ranked last, as their own "Archived" tier.
    private static int CategoryRank(ReportRow row, List<string> categoryOrder)
    {
        if (row.IsArchived) return categoryOrder.Count + 1;
        var index = categoryOrder.IndexOf(row.ColumnName);
        return index < 0 ? categoryOrder.Count : index;
    }

    // Sorts the flat row list by up to three keys before it's grouped downstream ("None" entries are
    // skipped). Category maps to the same ColumnName field GroupBy calls "Status" - the Add/Edit Task
    // dialog calls the column-selection field "Category", so this reuses that naming for consistency
    // in the UI even though the underlying model field is ColumnName.
    private static List<ReportRow> SortRows(List<ReportRow> rows, string sortLevel1, string sortLevel2, string sortLevel3, List<string> categoryOrder)
    {
        var keys = new[] { sortLevel1, sortLevel2, sortLevel3 }.Where(k => !string.IsNullOrEmpty(k) && k != "None").ToList();
        if (keys.Count == 0) return rows;

        var ordered = ApplyOrderBy(rows, keys[0], categoryOrder);
        for (var i = 1; i < keys.Count; i++)
        {
            ordered = ApplyThenBy(ordered, keys[i], categoryOrder);
        }
        return ordered.ToList();
    }

    private static IOrderedEnumerable<ReportRow> ApplyOrderBy(IEnumerable<ReportRow> rows, string key, List<string> categoryOrder) => key switch
    {
        "Category" => rows.OrderBy(r => CategoryRank(r, categoryOrder)),
        "Priority" => rows.OrderBy(r => Priorities.Rank(r.Priority)),
        "Who" => rows.OrderBy(LeadOf, StringComparer.OrdinalIgnoreCase),
        "Due Date" => rows.OrderBy(r => r.DueDate ?? DateTime.MaxValue),
        "Completed Date" => rows.OrderBy(r => r.CompletedAt ?? DateTime.MaxValue), // unfinished tasks last
        "Project" => rows.OrderBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase),
        "Goal" => rows.OrderBy(r => r.GoalName, StringComparer.OrdinalIgnoreCase),
        _ => rows.OrderBy(_ => 0)
    };

    private static IOrderedEnumerable<ReportRow> ApplyThenBy(IOrderedEnumerable<ReportRow> rows, string key, List<string> categoryOrder) => key switch
    {
        "Category" => rows.ThenBy(r => CategoryRank(r, categoryOrder)),
        "Priority" => rows.ThenBy(r => Priorities.Rank(r.Priority)),
        "Who" => rows.ThenBy(LeadOf, StringComparer.OrdinalIgnoreCase),
        "Due Date" => rows.ThenBy(r => r.DueDate ?? DateTime.MaxValue),
        "Completed Date" => rows.ThenBy(r => r.CompletedAt ?? DateTime.MaxValue),
        "Project" => rows.ThenBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase),
        "Goal" => rows.ThenBy(r => r.GoalName, StringComparer.OrdinalIgnoreCase),
        _ => rows
    };

    internal static List<IGrouping<string, ReportRow>> GroupRows(List<ReportRow> rows, string groupBy) => groupBy switch
    {
        "Status" => rows.GroupBy(r => r.ColumnName).ToList(),
        "Project" => rows.GroupBy(r => r.ProjectName).OrderBy(g => g.Key).ToList(),
        "Priority" => rows.GroupBy(r => r.Priority).OrderBy(g => g.Key).ToList(),
        // A shared task is listed under each of its people, not just the lead.
        "Who" => rows.SelectMany(r => WhoKeys(r).Select(name => (name, r))).GroupBy(x => x.name, x => x.r).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).ToList(),
        "Goal" => rows.GroupBy(r => r.GoalName).OrderBy(g => g.Key).ToList(),
        _ => rows.GroupBy(_ => string.Empty).ToList()
    };

    // Rows built before tasks could be shared have only Who.
    private static List<string> WhoKeys(ReportRow row) =>
        row.People.Count > 0 ? row.People : [string.IsNullOrWhiteSpace(row.Who) ? "Unassigned" : row.Who];

    private static string LeadOf(ReportRow row) => WhoKeys(row)[0];

    // whoGroup is the person whose section the row is being printed in, when the report is grouped
    // by Who: a shared task then says whose sections it also appears in.
    internal static List<string> BuildMetaParts(ReportRow row, string? whoGroup = null)
    {
        var parts = new List<string>
        {
            $"Status: {row.ColumnName}{(row.IsArchived ? " (Archived)" : "")}",
            $"Project: {row.ProjectName}",
            $"Priority: {row.Priority}"
        };

        if (row.StartDate is not null) parts.Add($"Start {row.StartDate:MMM d, yyyy}");
        if (!string.IsNullOrWhiteSpace(row.WaitingOn)) parts.Add($"Waiting on: {row.WaitingOn}");
        if (row.DueDate is not null) parts.Add($"Due {row.DueDate:MMM d, yyyy}");
        if (row.CompletedAt is not null) parts.Add($"Completed {row.CompletedAt:MMM d, yyyy h:mm tt}");
        var others = whoGroup is null ? [] : row.People.Where(p => !string.Equals(p, whoGroup, StringComparison.OrdinalIgnoreCase)).ToList();
        parts.Add(string.IsNullOrWhiteSpace(row.Who) ? "Unassigned"
            : others.Count == 0 ? $"Who: {row.Who}"
            : $"Who: {row.Who} (shared - also listed under {string.Join(", ", others)})");
        if (row.GoalName != "No Goal") parts.Add($"Goal: {row.GoalName}");
        if (row.Flags.Count > 0) parts.Add($"Flags: {string.Join(", ", row.Flags)}");
        if (row.SubTasks.Count > 0)
        {
            var done = row.SubTasks.Count(s => s.IsDone);
            parts.Add($"Sub-tasks: {done}/{row.SubTasks.Count}");
        }

        return parts;
    }

    private static string BuildStatusSummary(List<ReportRow> rows)
    {
        var counts = rows.GroupBy(r => r.IsArchived ? "Archived" : r.ColumnName)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {g.Count()}");
        return string.Join("   •   ", counts);
    }
}
