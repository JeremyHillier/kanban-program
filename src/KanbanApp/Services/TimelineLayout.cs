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
// EndAt is where the arrow's head stops, in columns from the left edge (2.43 = 43% across the third column):
// the end of the due day, so a task due on the first day of a week stops a seventh of the way into it.
public sealed record TimelineItem(CardViewModel Card, int FirstUnit, int LastUnit, int Lane, bool StartsBeforeWindow, bool DueAfterWindow, double EndAt = 0)
{
    public int BoxUnit => FirstUnit;

    public bool HasArrow => LastUnit > FirstUnit;

    // The columns the arrow runs across: from just after the box to the due column (its head
    // stops in the middle of that column, or at the edge when the due date is beyond it).
    public int ArrowFirstUnit => FirstUnit + 1;
    public int ArrowLastUnit => LastUnit;
}

// A Summary view bar: where it runs (in columns, clamped to the window like a task's arrow; StartAt and EndAt to the
// day, as fractions of columns: from the start of its first day to the end of its last) and what
// the project or goal holds.
public sealed record TimelineSummary(DateTime Start, DateTime End, int FirstUnit, int LastUnit, bool StartsBeforeWindow, bool EndsAfterWindow,
    int OpenTasks, int Overdue, int Undated, DateTime? Target = null, double? OverrunFrom = null, double StartAt = 0, double EndAt = 0)
{
    public string OpenText => $"{OpenTasks} open task{(OpenTasks == 1 ? "" : "s")}";

    public string OverdueText => Overdue > 0 ? $"{Overdue} overdue" : string.Empty;

    public string UndatedText => Undated > 0 ? $"{Undated} undated" : string.Empty;

    public string CountsText => string.Join(" · ", new[] { OpenText, OverdueText, UndatedText }.Where(s => s.Length > 0));

    // How far the last open task runs past the goal's target date, in words ("" when it does not).
    public string OverrunText
    {
        get
        {
            if (Target is not { } target || End.Date <= target.Date) return string.Empty;

            var days = (End.Date - target.Date).Days;
            var amount = days >= 14 ? $"{days / 7} weeks" : $"{days} day{(days == 1 ? "" : "s")}";
            return $"ends {amount} after target";
        }
    }

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
            var endAt = s.After ? unitStarts.Count : EndOffset(SpanOf(s.Card)!.Value.End, unitStarts, windowEnd);
            result.Add(new TimelineItem(s.Card, s.First, s.Last, lane, s.Before, s.After, endAt));
        }

        return result;
    }

    // The Summary view's one bar for a project or goal: from the earliest start or due date of its open
    // tasks to the latest. Null when none of them has a date, or the bar would fall outside the window.
    // The counts cover all of the group's open tasks, not just the ones in the window.
    public static TimelineSummary? Summarise(IEnumerable<CardViewModel> openCards, DateTime today, IReadOnlyList<DateTime> unitStarts, DateTime windowEnd, DateTime? target = null, DateTime? goalStart = null)
    {
        var cards = openCards.ToList();
        var spans = cards.Select(SpanOf).OfType<(DateTime Start, DateTime End)>().ToList();
        if (spans.Count == 0) return null;

        var start = spans.Min(s => s.Start);
        if (goalStart is { } begun && begun.Date < start) start = begun.Date; // the goal began before its first task
        var end = spans.Max(s => s.End);
        var windowStart = unitStarts[0];
        if (start >= windowEnd || end < windowStart) return null;

        var before = start < windowStart;
        var after = end >= windowEnd;
        var first = before ? 0 : TimelineScale.UnitIndex(unitStarts, start);
        var last = after ? unitStarts.Count - 1 : TimelineScale.UnitIndex(unitStarts, end);

        // The bar to the day: from the start of its first day to the end of its last.
        var startAt = before ? 0 : Offset(start, unitStarts, windowEnd);
        var endAt = after ? unitStarts.Count : EndOffset(end, unitStarts, windowEnd);

        // Where the bar turns red: the part of it after the end of the goal's target day, as a share of its
        // width. Null when it does not run past the target, or the target is beyond the right edge.
        double? overrunFrom = null;
        if (target is { } day && end.Date > day.Date)
        {
            if (day.Date < windowStart) overrunFrom = 0;
            else if (day.Date < windowEnd && endAt > startAt)
                overrunFrom = Math.Clamp((EndOffset(day, unitStarts, windowEnd) - startAt) / (endAt - startAt), 0, 1);
        }

        return new TimelineSummary(start, end, first, last, before, after,
            cards.Count,
            cards.Count(c => c.DueDate is { } due && due.Date < today.Date),
            cards.Count - spans.Count,
            target, overrunFrom is >= 1 ? null : overrunFrom, startAt, endAt);
    }

    // Where the start of a day falls, in columns from the left edge (clamped to the window): 2.5 is halfway
    // through the third column. Columns can differ in length (months), so each is measured on its own.
    public static double Offset(DateTime day, IReadOnlyList<DateTime> unitStarts, DateTime windowEnd)
    {
        var date = day.Date;
        if (date <= unitStarts[0]) return 0;
        if (date >= windowEnd) return unitStarts.Count;

        var unit = TimelineScale.UnitIndex(unitStarts, date);
        var unitEnd = unit + 1 < unitStarts.Count ? unitStarts[unit + 1] : windowEnd;
        var length = (unitEnd - unitStarts[unit]).TotalDays;
        return unit + (length <= 0 ? 0 : (date - unitStarts[unit]).TotalDays / length);
    }

    // Where the end of a day falls: a due date is the whole of that day.
    public static double EndOffset(DateTime day, IReadOnlyList<DateTime> unitStarts, DateTime windowEnd) =>
        Offset(day.Date.AddDays(1), unitStarts, windowEnd);

    // The column a goal's target date (or a milestone) falls in and how far through that column the end of
    // that day is (0 to 1), for its marker - so it lines up with an arrow due the same day. Null when the
    // date is outside the window.
    public static (int Unit, double Fraction)? TargetPosition(DateTime target, IReadOnlyList<DateTime> unitStarts, DateTime windowEnd)
    {
        var day = target.Date;
        if (day < unitStarts[0] || day >= windowEnd) return null;

        var unit = TimelineScale.UnitIndex(unitStarts, day);
        return (unit, EndOffset(day, unitStarts, windowEnd) - unit);
    }

    // The light lines inside a column: each day in a week column, each Monday in a month column (none in
    // Day view), as fractions across it.
    public static List<double> SubDivisions(TimelineZoom zoom, DateTime unitStart, DateTime unitEnd)
    {
        var length = (unitEnd - unitStart).TotalDays;
        return zoom switch
        {
            TimelineZoom.Week => Enumerable.Range(1, 6).Select(d => d / 7.0).ToList(),
            TimelineZoom.Month => Enumerable.Range(1, (int)length - 1).Select(d => unitStart.AddDays(d))
                .Where(d => d.DayOfWeek == DayOfWeek.Monday).Select(d => (d - unitStart).TotalDays / length).ToList(),
            _ => []
        };
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
