using System.Globalization;
using KanbanApp.Models;

namespace KanbanApp.Services;

// The periods a statistics report can cover. A saved view keeps the period's key, not its dates,
// so "Last month" is always last month whenever the report is run.
public static class StatisticsPeriods
{
    public const string Last7Days = "Last7";
    public const string Last30Days = "Last30";
    public const string ThisMonth = "ThisMonth";
    public const string LastMonth = "LastMonth";
    public const string ThisQuarter = "ThisQuarter";
    public const string LastQuarter = "LastQuarter";
    public const string ThisYear = "ThisYear";
    public const string LastYear = "LastYear";
    public const string AllTime = "AllTime";
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        (Last7Days, "Last 7 days"), (Last30Days, "Last 30 days"),
        (ThisMonth, "This month"), (LastMonth, "Last month"),
        (ThisQuarter, "This quarter"), (LastQuarter, "Last quarter"),
        (ThisYear, "This year"), (LastYear, "Last year"),
        (AllTime, "All time"), (Custom, "Custom dates")
    ];

    public static string Label(string key) => All.FirstOrDefault(p => p.Key == key).Label ?? "Last 30 days";

    // The first and last day the period covers, both included. All time starts at the earliest
    // task (or today when there is none); custom dates the wrong way round are put the right way.
    public static (DateTime From, DateTime To) Resolve(string key, DateTime today, DateTime? customFrom = null, DateTime? customTo = null, DateTime? earliest = null)
    {
        today = today.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        var quarter = new DateTime(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
        var year = new DateTime(today.Year, 1, 1);

        switch (key)
        {
            case Last7Days: return (today.AddDays(-6), today);
            case ThisMonth: return (month, today);
            case LastMonth: return (month.AddMonths(-1), month.AddDays(-1));
            case ThisQuarter: return (quarter, today);
            case LastQuarter: return (quarter.AddMonths(-3), quarter.AddDays(-1));
            case ThisYear: return (year, today);
            case LastYear: return (year.AddYears(-1), year.AddDays(-1));
            case AllTime: return (earliest is { } first && first.Date < today ? first.Date : today, today);
            case Custom:
                var from = (customFrom ?? customTo ?? today).Date;
                var to = (customTo ?? customFrom ?? today).Date;
                return from <= to ? (from, to) : (to, from);
            default: return (today.AddDays(-29), today);
        }
    }

    public static string Describe(DateTime from, DateTime to) =>
        from.Year == to.Year ? $"{from:MMM d} to {to:MMM d, yyyy}" : $"{from:MMM d, yyyy} to {to:MMM d, yyyy}";
}

public sealed record StatisticsOptions(DateTime From, DateTime To, string Breakdown, string OverTime);

public sealed record StatisticsSummary(int Added, int Finished, int FinishedWithDueDate, int FinishedOnTime, double? TypicalDaysToFinish, int OpenNow, int OverdueNow);

public sealed record StatisticsGroup(string Name, int Added, int Finished, int FinishedWithDueDate, int FinishedOnTime, double? TypicalDaysToFinish, int OpenNow, int OverdueNow);

public sealed record StatisticsBucket(string Label, DateTime Start, int Added, int Finished);

public sealed record StatisticsColumnTime(string Column, int Tasks, double TypicalDays, double LongestDays);

public sealed record TaskStatisticsResult(DateTime From, DateTime To, int TaskCount, StatisticsSummary Summary,
    IReadOnlyList<StatisticsGroup> Groups, IReadOnlyList<StatisticsBucket> Buckets, IReadOnlyList<StatisticsColumnTime> ColumnTimes);

// Works out a statistics report from the tasks a report's filters select (board and archived; a
// deleted task is in neither) and their history:
//   - added: put on the board in the period;
//   - finished: reached Done in the period (a task reopened since no longer counts as finished);
//   - on time: finished on or before its due date, out of those finished that had one;
//   - typical days to finish: the middle value from added to finished, so one long-forgotten task
//     doesn't drag it out the way an average would;
//   - open now / overdue now: as of today, whatever the period;
//   - time in each column: for tasks finished in the period, the days each spent in every column on
//     its way to Done, from its moves.
// A repeating task's occurrences are separate tasks, so each counts on its own.
public static class TaskStatistics
{
    public const string DoneColumn = "Done";

    public static TaskStatisticsResult Compute(IReadOnlyList<ReportRow> rows, IReadOnlyDictionary<int, List<TaskEvent>> history,
        IReadOnlyList<(string Key, string DisplayName)> columns, StatisticsOptions options, DateTime today)
    {
        today = today.Date;
        var tasks = rows.Select(r => new TaskFacts(r, history.TryGetValue(r.CardId, out var events) ? events : [], options, today)).ToList();

        return new TaskStatisticsResult(options.From, options.To, tasks.Count, Summarise(tasks),
            Groups(tasks, options.Breakdown), Buckets(tasks, options), ColumnTimes(tasks, columns));
    }

    private sealed class TaskFacts
    {
        public TaskFacts(ReportRow row, List<TaskEvent> events, StatisticsOptions options, DateTime today)
        {
            Row = row;
            Events = events;
            Added = events.FirstOrDefault(e => e.IsCreated)?.At;
            AddedInPeriod = Added is { } added && added.Date >= options.From && added.Date <= options.To;
            FinishedInPeriod = row.CompletedAt is { } done && done.Date >= options.From && done.Date <= options.To;
            HasDueDate = row.DueDate is not null;
            OnTime = FinishedInPeriod && row.DueDate is { } due && row.CompletedAt!.Value.Date <= due.Date;
            DaysToFinish = FinishedInPeriod && Added is { } start && row.CompletedAt!.Value >= start ? (row.CompletedAt.Value - start).TotalDays : null;
            OpenNow = !row.IsArchived && row.ColumnKey != DoneColumn;
            OverdueNow = OpenNow && row.DueDate is { } dueDate && dueDate.Date < today;
        }

        public ReportRow Row { get; }
        public List<TaskEvent> Events { get; }
        public DateTime? Added { get; }
        public bool AddedInPeriod { get; }
        public bool FinishedInPeriod { get; }
        public bool HasDueDate { get; }
        public bool OnTime { get; }
        public double? DaysToFinish { get; }
        public bool OpenNow { get; }
        public bool OverdueNow { get; }
    }

    private static StatisticsSummary Summarise(IReadOnlyList<TaskFacts> tasks) => new(
        tasks.Count(t => t.AddedInPeriod),
        tasks.Count(t => t.FinishedInPeriod),
        tasks.Count(t => t.FinishedInPeriod && t.HasDueDate),
        tasks.Count(t => t.OnTime),
        Median(tasks.Where(t => t.DaysToFinish is not null).Select(t => t.DaysToFinish!.Value)),
        tasks.Count(t => t.OpenNow),
        tasks.Count(t => t.OverdueNow));

    // One row per project, person, priority or goal that had anything to count. A shared task counts
    // under each of its people, as everywhere else in the app.
    private static List<StatisticsGroup> Groups(IReadOnlyList<TaskFacts> tasks, string breakdown)
    {
        if (breakdown is not ("Project" or "Who" or "Priority" or "Goal")) return [];

        IEnumerable<string> KeysOf(ReportRow row) => breakdown switch
        {
            "Project" => [row.ProjectName],
            "Priority" => [row.Priority],
            "Goal" => [row.GoalName],
            _ => row.People.Count == 0 ? ["Unassigned"] : row.People
        };

        var rank = tasks.GroupBy(t => t.Row.Priority).ToDictionary(g => g.Key, g => g.Min(t => t.Row.PriorityRank));
        var last = breakdown switch { "Project" => "No Project", "Goal" => "No Goal", "Who" => "Unassigned", _ => null };

        return tasks
            .SelectMany(t => KeysOf(t.Row).Distinct().Select(key => (Key: key, Task: t)))
            .GroupBy(x => x.Key)
            .Select(g =>
            {
                var group = g.Select(x => x.Task).ToList();
                var s = Summarise(group);
                return new StatisticsGroup(g.Key, s.Added, s.Finished, s.FinishedWithDueDate, s.FinishedOnTime, s.TypicalDaysToFinish, s.OpenNow, s.OverdueNow);
            })
            .Where(g => g.Added + g.Finished + g.OpenNow > 0)
            .OrderBy(g => g.Name == last)
            .ThenBy(g => breakdown == "Priority" ? rank.GetValueOrDefault(g.Name, int.MaxValue) : 0)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // Weeks start on Monday; a week or month at either end of the period counts only its days inside it.
    private static List<StatisticsBucket> Buckets(IReadOnlyList<TaskFacts> tasks, StatisticsOptions options)
    {
        var byMonth = options.OverTime == "Month";
        var start = byMonth
            ? new DateTime(options.From.Year, options.From.Month, 1)
            : options.From.AddDays(-(((int)options.From.DayOfWeek + 6) % 7));

        var buckets = new List<StatisticsBucket>();
        while (start <= options.To)
        {
            var next = byMonth ? start.AddMonths(1) : start.AddDays(7);
            bool InBucket(DateTime? when) => when is { } w && w.Date >= start && w.Date < next && w.Date >= options.From && w.Date <= options.To;

            var label = byMonth ? start.ToString("MMMM yyyy", CultureInfo.CurrentCulture) : $"Week of {start:MMM d, yyyy}";
            buckets.Add(new StatisticsBucket(label, start, tasks.Count(t => InBucket(t.Added)), tasks.Count(t => InBucket(t.Row.CompletedAt))));
            start = next;
        }
        return buckets;
    }

    // From each finished task's moves: the time between entering a column and leaving it, added up
    // per column (a task can visit one twice). Time in Done isn't counted - it is where the task
    // ends - nor time after the task was finished.
    private static List<StatisticsColumnTime> ColumnTimes(IReadOnlyList<TaskFacts> tasks, IReadOnlyList<(string Key, string DisplayName)> columns)
    {
        var daysByColumn = new Dictionary<string, List<double>>();

        foreach (var task in tasks.Where(t => t.FinishedInPeriod && t.Events.Count > 0))
        {
            var end = task.Row.CompletedAt!.Value;
            var perColumn = new Dictionary<string, double>();
            for (var i = 0; i < task.Events.Count; i++)
            {
                var entered = task.Events[i];
                if (entered.Column == DoneColumn || entered.At >= end) continue;

                var left = i + 1 < task.Events.Count ? task.Events[i + 1].At : end;
                if (left > end) left = end;
                perColumn[entered.Column] = perColumn.GetValueOrDefault(entered.Column) + Math.Max(0, (left - entered.At).TotalDays);
            }

            foreach (var (column, days) in perColumn)
            {
                if (!daysByColumn.TryGetValue(column, out var list)) daysByColumn[column] = list = [];
                list.Add(days);
            }
        }

        var order = columns.Select((c, i) => (c.Key, i)).ToDictionary(x => x.Key, x => x.i);
        var names = columns.ToDictionary(c => c.Key, c => c.DisplayName);
        return daysByColumn
            .OrderBy(kv => order.GetValueOrDefault(kv.Key, int.MaxValue))
            .Select(kv => new StatisticsColumnTime(names.GetValueOrDefault(kv.Key, kv.Key), kv.Value.Count, Median(kv.Value)!.Value, kv.Value.Max()))
            .ToList();
    }

    internal static double? Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return null;
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    // "under 1", "3", "12.5" - days, to one decimal place.
    public static string Days(double? days) =>
        days is not { } d ? "-" : d < 1 ? "under 1" : d.ToString("0.#", CultureInfo.CurrentCulture);

    // "85%", or "-" when nothing had a due date.
    public static string OnTimeShare(int onTime, int withDueDate) =>
        withDueDate == 0 ? "-" : $"{Math.Round(onTime * 100.0 / withDueDate):0}%";
}
