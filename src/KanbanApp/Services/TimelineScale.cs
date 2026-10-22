namespace KanbanApp.Services;

public enum TimelineZoom { Week, Day, Month }

// The Timeline's columns for each view: Week = 12 weeks paged 4 at a time, Day = 21 days paged a week
// at a time (both Monday-aligned), Month = 6 calendar months paged 3 at a time (starting on the 1st).
// Months are not all the same length, so a column is known by the day it starts, not by a day count.
public static class TimelineScale
{
    public const int WeekUnits = 12;
    public const int DayUnits = 21;
    public const int MonthUnits = 6;
    public const int MonthStep = 3;

    // The saved setting; anything else (including the old default) is Week.
    public static TimelineZoom Parse(string? saved) => saved switch
    {
        "Day" => TimelineZoom.Day,
        "Month" => TimelineZoom.Month,
        _ => TimelineZoom.Week
    };

    public static DateTime MondayOf(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    // Where a window starting on or around this date begins: the 1st of the month in Month view, else the Monday.
    public static DateTime Snap(TimelineZoom zoom, DateTime date) =>
        zoom == TimelineZoom.Month ? new DateTime(date.Year, date.Month, 1) : MondayOf(date);

    public static List<DateTime> UnitStarts(TimelineZoom zoom, DateTime windowStart) => zoom switch
    {
        TimelineZoom.Month => Enumerable.Range(0, MonthUnits).Select(windowStart.AddMonths).ToList(),
        TimelineZoom.Day => Enumerable.Range(0, DayUnits).Select(d => windowStart.AddDays(d)).ToList(),
        _ => Enumerable.Range(0, WeekUnits).Select(w => windowStart.AddDays(w * 7)).ToList()
    };

    // The first day after the window.
    public static DateTime End(TimelineZoom zoom, DateTime windowStart) => zoom switch
    {
        TimelineZoom.Month => windowStart.AddMonths(MonthUnits),
        TimelineZoom.Day => windowStart.AddDays(DayUnits),
        _ => windowStart.AddDays(WeekUnits * 7)
    };

    // One page earlier (-1) or later (+1).
    public static DateTime Step(TimelineZoom zoom, DateTime windowStart, int direction) => zoom switch
    {
        TimelineZoom.Month => windowStart.AddMonths(MonthStep * direction),
        TimelineZoom.Day => windowStart.AddDays(7 * direction),
        _ => windowStart.AddDays(28 * direction)
    };

    public static string StepLabel(TimelineZoom zoom) => zoom switch
    {
        TimelineZoom.Month => "3 Months",
        TimelineZoom.Day => "1 Week",
        _ => "4 Weeks"
    };

    public static string ViewName(TimelineZoom zoom) => $"{zoom} view";

    // The column a date inside the window falls in.
    public static int UnitIndex(IReadOnlyList<DateTime> unitStarts, DateTime date)
    {
        var index = unitStarts.Count - 1;
        while (index > 0 && unitStarts[index] > date.Date) index--;
        return index;
    }
}
