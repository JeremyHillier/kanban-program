using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Spawning a recurring task's next occurrence (triggered from MoveCard in MainViewModel.Cards.cs
// when a recurring card that hasn't spawned yet lands in Done) and the due-date math per pattern.
// A task set to happen a number of times passes one fewer to each new occurrence; the last one
// (RecurrencesLeft 1) creates nothing - see CardViewModel.HasNextOccurrence.
public partial class MainViewModel
{
    private void SpawnNextOccurrence(CardViewModel completedCard)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do");
        if (toDoColumn is null) return;

        var range = MonthDayRange.Parse(completedCard.RecurrencePattern);
        var nextDueDate = range is not null
            ? range.NextAfter(completedCard.DueDate, DateTime.Today)
            : CalculateNextDueDate(completedCard.DueDate ?? DateTime.Today, completedCard.RecurrencePattern!);

        // Days of each month: one copy per day, so a start date is simply that day; and a count
        // counts months, going down only when the next copy is in a later month.
        var recurrencesLeft = completedCard.RecurrencesLeft - 1;
        if (range is not null)
        {
            var laterMonth = MonthDayRange.IsLaterMonth(completedCard.DueDate, nextDueDate, DateTime.Today);
            if (!laterMonth) recurrencesLeft = completedCard.RecurrencesLeft;
            else if (completedCard.RecurrencesLeft is <= 1) return; // the last month is over
        }

        // A start date moves on with the task, keeping the same lead time before the due date.
        // (With no due date to measure from, it simply steps forward by the pattern.)
        DateTime? nextStartDate = completedCard.StartDate is not { } start ? null
            : completedCard.DueDate is { } due ? nextDueDate - (due.Date - start.Date)
            : CalculateNextDueDate(start, completedCard.RecurrencePattern!);
        if (nextStartDate > nextDueDate) nextStartDate = nextDueDate;
        if (range is not null && nextStartDate is not null) nextStartDate = nextDueDate;

        var project = Projects.FirstOrDefault(p => p.Id == completedCard.ProjectId);
        var goal = Goals.FirstOrDefault(g => g.Id == completedCard.GoalId);
        var who = People.FirstOrDefault(p => p.Id == completedCard.WhoId);
        var freshSubTasks = completedCard.SubTasks
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = false }))
            .ToList();

        AddCard(completedCard.Title, toDoColumn, project, completedCard.Priority, nextDueDate, who,
            true, completedCard.RecurrencePattern, goal, completedCard.Flags, freshSubTasks, completedCard.Notes,
            forceEditOnComplete: completedCard.ForceEditOnComplete, websiteUrl: completedCard.WebsiteUrl, dueTime: completedCard.DueTime,
            startDate: nextStartDate, people: [.. completedCard.People], recurrencesLeft: recurrencesLeft);
    }

    internal static DateTime CalculateNextDueDate(DateTime anchor, string pattern) =>
        RecurrenceInterval.Parse(pattern) is { } every ? every.Next(anchor) : NextByPattern(anchor, pattern);

    private static DateTime NextByPattern(DateTime anchor, string pattern) => pattern switch
    {
        "Daily" => anchor.AddDays(1),
        "Weekday" => NextWeekday(anchor),
        "Weekly" => anchor.AddDays(7),
        "Bi-Weekly" => anchor.AddDays(14),
        "Semi-Monthly" => NextSemiMonthly(anchor),
        "Monthly" => anchor.AddMonths(1),
        "Bi-Monthly" => anchor.AddMonths(2),
        "Quarterly" => anchor.AddMonths(3),
        "Annually" => anchor.AddYears(1),
        _ => anchor.AddDays(1)
    };

    private static DateTime NextWeekday(DateTime date)
    {
        var next = date.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }
        return next;
    }

    // "Twice a month" deliberately isn't "every 15 days" - that would drift through the calendar and
    // land on a different pair of dates every month. Instead it pins the task to two fixed days of
    // the month, taken from whatever due date you set: a task due on the 3rd repeats on the 3rd and
    // the 18th, one due on the 20th repeats on the 5th and the 20th.
    //
    // So a first-half day (1-15) advances 15 days within the same month, and a second-half day steps
    // back 15 and moves to the next month.
    //
    // The end of the month is special-cased, because 15 + 15 overshoots every month and the 31st has
    // no partner in the 1-15 range. Any last-day-of-month anchor pairs with the 15th, which is both
    // the conventional semi-monthly pairing and the only rule that survives February: without it, a
    // 15th/30th task clamps to the 28th in February and then drifts to 13th/28th for good.
    private static DateTime NextSemiMonthly(DateTime anchor)
    {
        if (anchor.Day <= 15) return OnDayOfMonth(anchor.Year, anchor.Month, anchor.Day + 15, anchor.TimeOfDay);

        var nextMonth = new DateTime(anchor.Year, anchor.Month, 1).AddMonths(1);
        var isLastDayOfMonth = anchor.Day == DateTime.DaysInMonth(anchor.Year, anchor.Month);
        var day = isLastDayOfMonth ? 15 : anchor.Day - 15;
        return OnDayOfMonth(nextMonth.Year, nextMonth.Month, day, anchor.TimeOfDay);
    }

    // Days that don't exist in a short month fall back to that month's last day - the 30th becomes
    // the 28th in February - which is how AddMonths already behaves for the Monthly patterns above.
    private static DateTime OnDayOfMonth(int year, int month, int day, TimeSpan timeOfDay) =>
        new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month))).Add(timeOfDay);
}

// A Days of each month or Every N task means nothing to copies of the app from before it: they would repeat
// it daily for ever. So the file is stamped with the format that knows it the first time one is saved.
public partial class MainViewModel
{
    private void StampDayRange(bool isRecurring, string? recurrencePattern)
    {
        if (!isRecurring) return;
        if (MonthDayRange.Parse(recurrencePattern) is not null) _db.RaiseFileFormat(Services.DatabaseService.DayRangeFileFormat);
        if (RecurrenceInterval.Parse(recurrencePattern) is not null) _db.RaiseFileFormat(Services.DatabaseService.RecurrenceIntervalFileFormat);
    }
}
