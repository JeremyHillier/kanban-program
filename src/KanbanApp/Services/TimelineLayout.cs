using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Where one task goes on a Timeline row. A task with a start date has its box at the start (the
// left), and an arrow running right along the row to its due date. A task with no start date is
// just a box at its due date. Units are the Timeline's columns (days or weeks), counted from the
// left edge of the visible window.
//
// When the start is before the left edge there is no start column to put the box in, so the box
// goes in the first visible column instead, drawn dashed to show it isn't at its real start. When
// the due date is past the right edge, the arrow simply runs off that edge.
public sealed record TimelineItem(CardViewModel Card, int FirstUnit, int LastUnit, int Lane, bool StartsBeforeWindow, bool DueAfterWindow)
{
    public int BoxUnit => FirstUnit;

    public bool HasArrow => LastUnit > FirstUnit;

    // The columns the arrow runs across: from just after the box to the due column (its head
    // stops in the middle of that column, or at the edge when the due date is beyond it).
    public int ArrowFirstUnit => FirstUnit + 1;
    public int ArrowLastUnit => LastUnit;
}

// A Summary view bar: where it runs (in columns, clamped to the window like a task's arrow) and what
// the project or goal holds.
public sealed record TimelineSummary(DateTime Start, DateTime End, int FirstUnit, int LastUnit, bool StartsBeforeWindow, bool EndsAfterWindow,
    int OpenTasks, int Overdue, int Undated)
{
    public string OpenText => $"{OpenTasks} open task{(OpenTasks == 1 ? "" : "s")}";

    public string OverdueText => Overdue > 0 ? $"{Overdue} overdue" : string.Empty;

    public string UndatedText => Undated > 0 ? $"{Undated} undated" : string.Empty;

    public string CountsText => string.Join(" · ", new[] { OpenText, OverdueText, UndatedText }.Where(s => s.Length > 0));

    public string DatesText =>
        Start == End ? Start.ToString("MMM d, yyyy")
        : Start.Year == End.Year ? $"{Start:MMM d} – {End:MMM d, yyyy}"
        : $"{Start:MMM d, yyyy} – {End:MMM d, yyyy}";
}

public static class TimelineLayout
{
    // A task with no start date spans just its due date; one with only a start date, just that.
    public static (DateTime Start, DateTime End)? SpanOf(CardViewModel card)
    {
        var due = card.DueDate?.Date;
        var start = card.StartDate?.Date;
        if (due is null && start is null) return null;

        var from = start ?? due!.Value;
        var to = due ?? start!.Value;
        return from <= to ? (from, to) : (to, to);
    }

    public static bool IsInWindow(CardViewModel card, DateTime windowStart, DateTime windowEnd) =>
        SpanOf(card) is { } span && span.Start < windowEnd && span.End >= windowStart;

    // Lays out one row's tasks in lanes so that nothing overlaps: tasks with an arrow are placed
    // first (so long arrows sit together at the top of the row rather than being pushed down by
    // single boxes), then the rest, each into the first lane with room.
    public static List<TimelineItem> Place(IEnumerable<CardViewModel> cards, DateTime windowStart, int unitDays, int unitsToShow) =>
        Place(cards, Enumerable.Range(0, unitsToShow).Select(u => windowStart.AddDays(u * unitDays)).ToList(), windowStart.AddDays(unitDays * unitsToShow));

    // The same, for columns of any length (Month view): each column is known by the day it starts.
    public static List<TimelineItem> Place(IEnumerable<CardViewModel> cards, IReadOnlyList<DateTime> unitStarts, DateTime windowEnd)
    {
        var windowStart = unitStarts[0];
        var spans = new List<(CardViewModel Card, int First, int Last, bool Before, bool After)>();

        foreach (var card in cards)
        {
            if (SpanOf(card) is not { } span || span.Start >= windowEnd || span.End < windowStart) continue;

            var before = span.Start < windowStart;
            var after = span.End >= windowEnd;
            var first = before ? 0 : TimelineScale.UnitIndex(unitStarts, span.Start);
            var last = after ? unitStarts.Count - 1 : TimelineScale.UnitIndex(unitStarts, span.End);
            spans.Add((card, first, last, before, after));
        }

        var ordered = spans
            .OrderBy(s => s.Last == s.First) // arrows first
            .ThenBy(s => s.First)
            .ThenByDescending(s => s.Last - s.First)
            .ThenBy(s => s.Card.DueDate ?? s.Card.StartDate)
            .ThenBy(s => s.Card.Title, StringComparer.OrdinalIgnoreCase);

        var lanes = new List<List<(int First, int Last)>>();
        var result = new List<TimelineItem>();
        foreach (var s in ordered)
        {
            var lane = lanes.FindIndex(taken => taken.All(t => s.Last < t.First || s.First > t.Last));
            if (lane < 0)
            {
                lanes.Add([]);
                lane = lanes.Count - 1;
            }

            lanes[lane].Add((s.First, s.Last));
            result.Add(new TimelineItem(s.Card, s.First, s.Last, lane, s.Before, s.After));
        }

        return result;
    }

    // The Summary view's one bar for a project or goal: from the earliest start or due date of its open
    // tasks to the latest. Null when none of them has a date, or the bar would fall outside the window.
    // The counts cover all of the group's open tasks, not just the ones in the window.
    public static TimelineSummary? Summarise(IEnumerable<CardViewModel> openCards, DateTime today, IReadOnlyList<DateTime> unitStarts, DateTime windowEnd)
    {
        var cards = openCards.ToList();
        var spans = cards.Select(SpanOf).OfType<(DateTime Start, DateTime End)>().ToList();
        if (spans.Count == 0) return null;

        var start = spans.Min(s => s.Start);
        var end = spans.Max(s => s.End);
        var windowStart = unitStarts[0];
        if (start >= windowEnd || end < windowStart) return null;

        var before = start < windowStart;
        var after = end >= windowEnd;
        return new TimelineSummary(start, end,
            before ? 0 : TimelineScale.UnitIndex(unitStarts, start),
            after ? unitStarts.Count - 1 : TimelineScale.UnitIndex(unitStarts, end),
            before, after,
            cards.Count,
            cards.Count(c => c.DueDate is { } due && due.Date < today.Date),
            cards.Count - spans.Count);
    }

    // "Oct 20"; "Oct 3 to Oct 20" with a start date; "starts Oct 3" with only a start date.
    public static string DateLabel(CardViewModel card) => (card.StartDate, card.DueDate) switch
    {
        ({ } start, { } due) => $"{start:MMM d} to {due:MMM d}",
        ({ } start, null) => $"starts {start:MMM d}",
        (null, { } due) => due.ToString("MMM d"),
        _ => string.Empty
    };
}
