using System.Globalization;

namespace KanbanApp.Services;

public enum CalendarScale { Month, Week, Day }

// The dates the Calendar view shows for a Month, Week or Day, how the arrows page, and how it is titled. Weeks
// start on Monday, as on the Gantt view. Free of the window so it can be checked on its own.
public static class CalendarLayout
{
    public static CalendarScale Parse(string? saved) => Enum.TryParse<CalendarScale>(saved, out var mode) ? mode : CalendarScale.Month;

    public static DateTime MondayOf(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    // The first day shown and the first day after the last one. A month shows whole weeks, so it
    // takes in the end of the month before and the start of the month after.
    public static (DateTime Start, DateTime End) Range(CalendarScale mode, DateTime anchor)
    {
        switch (mode)
        {
            case CalendarScale.Day:
                return (anchor.Date, anchor.Date.AddDays(1));
            case CalendarScale.Week:
                var monday = MondayOf(anchor);
                return (monday, monday.AddDays(7));
            default:
                var first = new DateTime(anchor.Year, anchor.Month, 1);
                var last = first.AddMonths(1).AddDays(-1);
                return (MondayOf(first), MondayOf(last).AddDays(7));
        }
    }

    // What the previous and next arrows go to: a month, a week or a day away.
    public static DateTime Step(CalendarScale mode, DateTime anchor, int direction) => mode switch
    {
        CalendarScale.Day => anchor.Date.AddDays(direction),
        CalendarScale.Week => anchor.Date.AddDays(7 * direction),
        _ => new DateTime(anchor.Year, anchor.Month, 1).AddMonths(direction)
    };

    public static string Title(CalendarScale mode, DateTime anchor)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (mode)
        {
            case CalendarScale.Day:
                return anchor.ToString("dddd, MMMM d, yyyy", culture);
            case CalendarScale.Week:
                var (start, end) = Range(mode, anchor);
                var last = end.AddDays(-1);
                return start.Year == last.Year
                    ? $"{start.ToString("MMM d", culture)} – {last.ToString("MMM d, yyyy", culture)}"
                    : $"{start.ToString("MMM d, yyyy", culture)} – {last.ToString("MMM d, yyyy", culture)}";
            default:
                return anchor.ToString("MMMM yyyy", culture);
        }
    }

    // The days to draw, in weeks of seven (or five without Saturday and Sunday), oldest first.
    public static List<List<DateTime>> Weeks(DateTime start, DateTime end, bool weekends)
    {
        var weeks = new List<List<DateTime>>();
        for (var monday = MondayOf(start); monday < end; monday = monday.AddDays(7))
        {
            var days = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).Where(d => d >= start && d < end).ToList();
            if (!weekends) days = days.Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
            if (days.Count > 0) weeks.Add(days);
        }
        return weeks;
    }

    // A task is overdue when its due day has passed and it is not finished.
    public static bool IsOverdue(DateTime? due, bool isDone, DateTime today) => due is { } day && !isDone && day.Date < today.Date;
}
