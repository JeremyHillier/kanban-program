using System.Globalization;

namespace KanbanApp.Services;

public enum CalendarScale { Month, Week, Day }

// The dates the Calendar view shows for a Month, Week or Day, how the arrows page, and how it is titled. Weeks
// start on Monday unless the user chose Sunday (Settings). Free of the window so it can be checked on its own.
public static class CalendarLayout
{
    public static CalendarScale Parse(string? saved) => Enum.TryParse<CalendarScale>(saved, out var mode) ? mode : CalendarScale.Month;

    public static DateTime WeekStartOf(DateTime date, DayOfWeek firstDay) =>
        date.Date.AddDays(-(((int)date.DayOfWeek - (int)firstDay + 7) % 7));

    public static DateTime MondayOf(DateTime date) => WeekStartOf(date, DayOfWeek.Monday);

    // The first day shown and the first day after the last one. A month shows whole weeks, so it
    // takes in the end of the month before and the start of the month after.
    public static (DateTime Start, DateTime End) Range(CalendarScale mode, DateTime anchor, DayOfWeek firstDay = DayOfWeek.Monday)
    {
        switch (mode)
        {
            case CalendarScale.Day:
                return (anchor.Date, anchor.Date.AddDays(1));
            case CalendarScale.Week:
                var weekStart = WeekStartOf(anchor, firstDay);
                return (weekStart, weekStart.AddDays(7));
            default:
                var first = new DateTime(anchor.Year, anchor.Month, 1);
                var last = first.AddMonths(1).AddDays(-1);
                return (WeekStartOf(first, firstDay), WeekStartOf(last, firstDay).AddDays(7));
        }
    }

    // The Month view scrolls on without stopping: whole weeks from `monthsBefore` months before the anchor's month to
    // the end of the month `monthsAfter` after it. The window adds more at either end as it is scrolled.
    public static (DateTime Start, DateTime End) ContinuousRange(DateTime anchor, int monthsBefore, int monthsAfter, DayOfWeek firstDay)
    {
        var first = new DateTime(anchor.Year, anchor.Month, 1);
        var lastMonth = first.AddMonths(monthsAfter);
        var last = lastMonth.AddMonths(1).AddDays(-1);
        return (WeekStartOf(first.AddMonths(-monthsBefore), firstDay), WeekStartOf(last, firstDay).AddDays(7));
    }

    // What the previous and next arrows go to: a month, a week or a day away.
    public static DateTime Step(CalendarScale mode, DateTime anchor, int direction) => mode switch
    {
        CalendarScale.Day => anchor.Date.AddDays(direction),
        CalendarScale.Week => anchor.Date.AddDays(7 * direction),
        _ => new DateTime(anchor.Year, anchor.Month, 1).AddMonths(direction)
    };

    public static string Title(CalendarScale mode, DateTime anchor, DayOfWeek firstDay = DayOfWeek.Monday)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (mode)
        {
            case CalendarScale.Day:
                return anchor.ToString("dddd, MMMM d, yyyy", culture);
            case CalendarScale.Week:
                var (start, end) = Range(mode, anchor, firstDay);
                var last = end.AddDays(-1);
                return start.Year == last.Year
                    ? $"{start.ToString("MMM d", culture)} – {last.ToString("MMM d, yyyy", culture)}"
                    : $"{start.ToString("MMM d, yyyy", culture)} – {last.ToString("MMM d, yyyy", culture)}";
            default:
                return anchor.ToString("MMMM yyyy", culture);
        }
    }

    // The days to draw, in weeks of seven (or five without Saturday and Sunday), oldest first.
    public static List<List<DateTime>> Weeks(DateTime start, DateTime end, bool weekends, DayOfWeek firstDay = DayOfWeek.Monday)
    {
        var weeks = new List<List<DateTime>>();
        for (var weekStart = WeekStartOf(start, firstDay); weekStart < end; weekStart = weekStart.AddDays(7))
        {
            var days = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).Where(d => d >= start && d < end).ToList();
            if (!weekends) days = days.Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
            if (days.Count > 0) weeks.Add(days);
        }
        return weeks;
    }

    // A task is overdue when its due day has passed and it is not finished.
    public static bool IsOverdue(DateTime? due, bool isDone, DateTime today) => due is { } day && !isDone && day.Date < today.Date;
}

// How the Calendar's No Due Date list is ordered.
public enum NoDueOrder { TitleAscending, TitleDescending, ByProject, ByGoal }
