using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Every number the Dashboard shows, worked out from the board (and the completion dates of archived
// tasks) without any drawing, so it can be tested on its own. DashboardWindow draws it.
public sealed class DashboardData
{
    public const string DoneColumn = "Done";
    public const string Unassigned = "Unassigned";
    public const int WeeksShown = 12;
    public const int MaxProjectRows = 20; // past this, the smallest projects fold into one "Other" row

    // A board card with the column it sits in (Name drives the colour; DisplayName is what's shown).
    public sealed record BoardCard(CardViewModel Card, string ColumnName, string ColumnDisplayName);

    public sealed record Status(string Name, string DisplayName);

    // One bar: its label and a count per series (statuses, or priorities), in series order.
    public sealed record Stack(string Label, IReadOnlyList<int> Counts)
    {
        public int Total => Counts.Sum();
    }

    public sealed record Bucket(string Label, int Count);

    public sealed record Week(DateTime Start, int Count);

    public int Open { get; private init; }
    public int Overdue { get; private init; }
    public int DueToday { get; private init; }
    public int DueThisWeek { get; private init; }
    public int WaitingOn { get; private init; }
    public int InDone { get; private init; }
    public int DoneLast7Days { get; private init; }

    public IReadOnlyList<Status> Statuses { get; private init; } = [];        // every column, board order
    public IReadOnlyList<Status> OpenStatuses { get; private init; } = [];    // every column but Done
    public IReadOnlyList<string> Priorities { get; private init; } = PriorityOptions.All;

    public IReadOnlyList<Stack> StatusByPriority { get; private init; } = [];  // a bar per column, split by priority
    public IReadOnlyList<Bucket> DueDates { get; private init; } = [];         // open tasks by when they're due
    public IReadOnlyList<Week> CompletedPerWeek { get; private init; } = [];   // oldest first; the last is this week
    public IReadOnlyList<Bucket> LastUpdated { get; private init; } = [];      // open tasks by time since last change
    public IReadOnlyList<Stack> ProjectsByStatus { get; private init; } = [];  // a bar per project, split by column
    public IReadOnlyList<Stack> PeopleByStatus { get; private init; } = [];    // open tasks per person, split by open column
    public int FoldedProjectCount { get; private init; }                       // how many projects the "Other" row holds

    public static DashboardData Build(IReadOnlyList<BoardCard> cards, IReadOnlyList<Status> columns,
        IEnumerable<DateTime> archivedCompletions, DateTime today, DayOfWeek firstDayOfWeek)
    {
        today = today.Date;
        var open = cards.Where(c => c.ColumnName != DoneColumn).Select(c => c.Card).ToList();
        var openStatuses = columns.Where(s => s.Name != DoneColumn).ToList();

        bool Overdue(CardViewModel c) => c.DueDate?.Date < today;

        // Completed = in Done on the board, or archived after finishing; each counts on the day it finished.
        var completions = cards.Where(c => c.ColumnName == DoneColumn && c.Card.CompletedAt is not null)
            .Select(c => c.Card.CompletedAt!.Value.Date)
            .Concat(archivedCompletions.Select(d => d.Date))
            .ToList();

        var thisWeekStart = today.AddDays(-(((int)today.DayOfWeek - (int)firstDayOfWeek + 7) % 7));
        var weeks = Enumerable.Range(0, WeeksShown)
            .Select(i => thisWeekStart.AddDays(-7 * (WeeksShown - 1 - i)))
            .Select(start => new Week(start, completions.Count(d => d >= start && d < start.AddDays(7))))
            .ToList();

        var projects = cards.GroupBy(c => c.Card.ProjectName)
            .Select(g => new Stack(g.Key, columns.Select(s => g.Count(c => c.ColumnName == s.Name)).ToList()))
            .OrderByDescending(s => s.Total).ThenBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var folded = 0;
        if (projects.Count > MaxProjectRows)
        {
            var kept = projects.Take(MaxProjectRows - 1).ToList();
            var rest = projects.Skip(MaxProjectRows - 1).ToList();
            folded = rest.Count;
            kept.Add(new Stack($"Other ({folded} projects)",
                columns.Select((_, i) => rest.Sum(p => p.Counts[i])).ToList()));
            projects = kept;
        }

        // A task shared by several people counts once for each of them.
        var people = cards.Where(c => c.ColumnName != DoneColumn)
            .SelectMany(c => (c.Card.People.Count == 0 ? [Unassigned] : c.Card.People.Select(p => p.Name)).Select(name => (Name: name, c.ColumnName)))
            .GroupBy(x => x.Name)
            .Select(g => new Stack(g.Key, openStatuses.Select(s => g.Count(x => x.ColumnName == s.Name)).ToList()))
            .OrderByDescending(s => s.Total).ThenBy(s => s.Label == Unassigned).ThenBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int AgeInDays(CardViewModel c) => c.LastUpdated is { } updated ? (today - updated.Date).Days : int.MaxValue;

        return new DashboardData
        {
            Open = open.Count,
            Overdue = open.Count(Overdue),
            DueToday = open.Count(c => c.DueDate?.Date == today),
            DueThisWeek = open.Count(c => c.DueDate?.Date >= today && c.DueDate?.Date <= today.AddDays(7)),
            WaitingOn = open.Count(c => c.IsWaiting),
            InDone = cards.Count(c => c.ColumnName == DoneColumn),
            DoneLast7Days = completions.Count(d => d > today.AddDays(-7) && d <= today),

            Statuses = columns,
            OpenStatuses = openStatuses,
            StatusByPriority = columns
                .Select(s => new Stack(s.DisplayName, PriorityOptions.All.Select(p => cards.Count(c => c.ColumnName == s.Name && c.Card.Priority == p)).ToList()))
                .ToList(),
            DueDates =
            [
                new("Overdue", open.Count(Overdue)),
                new("Today", open.Count(c => c.DueDate?.Date == today)),
                new("Next 7 days", open.Count(c => c.DueDate?.Date > today && c.DueDate?.Date <= today.AddDays(7))),
                new("8–30 days", open.Count(c => c.DueDate?.Date > today.AddDays(7) && c.DueDate?.Date <= today.AddDays(30))),
                new("Later", open.Count(c => c.DueDate?.Date > today.AddDays(30))),
                new("No due date", open.Count(c => c.DueDate is null)),
            ],
            CompletedPerWeek = weeks,
            LastUpdated =
            [
                new("Under a week", open.Count(c => AgeInDays(c) < 7)),
                new("1–4 weeks", open.Count(c => AgeInDays(c) is >= 7 and < 30)),
                new("1–3 months", open.Count(c => AgeInDays(c) is >= 30 and < 90)),
                new("Over 3 months", open.Count(c => AgeInDays(c) >= 90)),
            ],
            ProjectsByStatus = projects,
            FoldedProjectCount = folded,
            PeopleByStatus = people,
        };
    }
}
