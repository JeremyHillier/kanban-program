using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Where one task goes on a Timeline row. The task's box sits in the column of its due date, and a
// task with a start date gets an arrow running along the row from the start to that box. Units are
// the Timeline's columns (days or weeks), counted from the left edge of the visible window.
//
// When the due date is past the right edge there is no due column to put the box in, so the box
// goes at the left end of what's visible instead (drawn dashed, to show it isn't at its due date)
// and the arrow runs from it off the right edge.
public sealed record TimelineItem(CardViewModel Card, int FirstUnit, int LastUnit, int Lane, bool StartsBeforeWindow, bool DueAfterWindow)
{
    public int BoxUnit => DueAfterWindow ? FirstUnit : LastUnit;

    public bool HasArrow => LastUnit > FirstUnit;

    // The columns the arrow runs across - every column of the span except the box's.
    public int ArrowFirstUnit => DueAfterWindow ? FirstUnit + 1 : FirstUnit;
    public int ArrowLastUnit => DueAfterWindow ? LastUnit : LastUnit - 1;
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
    public static List<TimelineItem> Place(IEnumerable<CardViewModel> cards, DateTime windowStart, int unitDays, int unitsToShow)
    {
        var windowEnd = windowStart.AddDays(unitDays * unitsToShow);
        var spans = new List<(CardViewModel Card, int First, int Last, bool Before, bool After)>();

        foreach (var card in cards)
        {
            if (SpanOf(card) is not { } span || span.Start >= windowEnd || span.End < windowStart) continue;

            var before = span.Start < windowStart;
            var after = span.End >= windowEnd;
            var first = before ? 0 : (span.Start - windowStart).Days / unitDays;
            var last = after ? unitsToShow - 1 : (span.End - windowStart).Days / unitDays;
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

    // "Oct 20"; "Oct 3 to Oct 20" with a start date; "starts Oct 3" with only a start date.
    public static string DateLabel(CardViewModel card) => (card.StartDate, card.DueDate) switch
    {
        ({ } start, { } due) => $"{start:MMM d} to {due:MMM d}",
        ({ } start, null) => $"starts {start:MMM d}",
        (null, { } due) => due.ToString("MMM d"),
        _ => string.Empty
    };
}
