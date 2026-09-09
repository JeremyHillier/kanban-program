using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Spawning a recurring task's next occurrence (triggered from MoveCard in MainViewModel.Cards.cs
// when a recurring card that hasn't spawned yet lands in Done) and the due-date math per pattern.
public partial class MainViewModel
{
    private void SpawnNextOccurrence(CardViewModel completedCard)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do");
        if (toDoColumn is null) return;

        var nextDueDate = CalculateNextDueDate(completedCard.DueDate ?? DateTime.Today, completedCard.RecurrencePattern!);
        var project = Projects.FirstOrDefault(p => p.Id == completedCard.ProjectId);
        var goal = Goals.FirstOrDefault(g => g.Id == completedCard.GoalId);
        var who = People.FirstOrDefault(p => p.Id == completedCard.WhoId);
        var freshSubTasks = completedCard.SubTasks
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = false }))
            .ToList();

        AddCard(completedCard.Title, toDoColumn, project, completedCard.Priority, nextDueDate, who,
            true, completedCard.RecurrencePattern, goal, completedCard.Flags, freshSubTasks, completedCard.Notes,
            forceEditOnComplete: completedCard.ForceEditOnComplete, websiteUrl: completedCard.WebsiteUrl);
    }

    private static DateTime CalculateNextDueDate(DateTime anchor, string pattern) => pattern switch
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
