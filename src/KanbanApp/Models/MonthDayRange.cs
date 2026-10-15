using System.Globalization;
using System.Text.RegularExpressions;

namespace KanbanApp.Models;

// "Days of each month": a task that repeats every day within part of each month, then waits for
// the next month - days 5 to 12, or the first 10 working days (Monday to Friday; the app knows no
// public holidays). Stored in Cards.RecurrencePattern as readable text ("Days 5-12 of each month",
// "Working days 1-10 of each month"), so emails, Excel files, reports and templates carry it as is.
//
// In a short month a calendar range stops at the month's end, and a month the range doesn't
// reach at all (days 30-31 in February) is skipped. A working-day range past the month's last
// working day stops there the same way.
public sealed partial record MonthDayRange(int From, int To, bool WorkingDays)
{
    // The entry in the task screen's Repeats list that leads to the From/To line.
    public const string ChoiceName = "Days of month";

    public const int Largest = 31;

    public override string ToString() =>
        $"{(WorkingDays ? "Working days" : "Days")} {From.ToString(CultureInfo.InvariantCulture)}-{To.ToString(CultureInfo.InvariantCulture)} of each month";

    [GeneratedRegex(@"^\s*(working\s+days|days)\s+(\d{1,2})\s*-\s*(\d{1,2})\s+of\s+each\s+month\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    // Null for anything that isn't one (including a range the wrong way round or out of 1-31).
    public static MonthDayRange? Parse(string? text)
    {
        var match = Pattern().Match(text ?? string.Empty);
        if (!match.Success) return null;

        var from = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var to = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        return from is >= 1 and <= Largest && to >= from && to <= Largest
            ? new MonthDayRange(from, to, match.Groups[1].Value.StartsWith("w", StringComparison.OrdinalIgnoreCase))
            : null;
    }

    // The days of one month that are in the range, earliest first.
    public IEnumerable<DateTime> DaysIn(int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        var workingDay = 0;
        for (var day = 1; day <= days; day++)
        {
            var date = new DateTime(year, month, day);
            if (WorkingDays)
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                workingDay++;
                if (workingDay > To) yield break;
                if (workingDay >= From) yield return date;
            }
            else if (day >= From && day <= To)
            {
                yield return date;
            }
        }
    }

    public bool Contains(DateTime date) => DaysIn(date.Year, date.Month).Contains(date.Date);

    // The first day in the range on or after date.
    public DateTime FirstOnOrAfter(DateTime date)
    {
        var month = new DateTime(date.Year, date.Month, 1);
        for (var i = 0; i < 24; i++, month = month.AddMonths(1))
        {
            foreach (var day in DaysIn(month.Year, month.Month))
            {
                if (day >= date.Date) return day;
            }
        }
        return date.Date; // unreachable for a valid range: every month has a 1st and a first working day
    }

    // The day after a finished copy that was due on dueDate. A missed day catches up to today rather
    // than leaving a run of past days to tick off one by one.
    public DateTime NextAfter(DateTime? dueDate, DateTime today)
    {
        var dayAfter = (dueDate ?? today).Date.AddDays(1);
        return FirstOnOrAfter(dayAfter > today.Date ? dayAfter : today.Date);
    }

    // A count of "times" on this kind of task counts months, so it only goes down when the next
    // copy falls in a later month than the one just finished.
    public static bool IsLaterMonth(DateTime? finished, DateTime next, DateTime today)
    {
        var from = (finished ?? today).Date;
        return next.Year * 12 + next.Month > from.Year * 12 + from.Month;
    }
}
