namespace KanbanApp.Models;

// The ways a recurring task can repeat, in the order the task screen lists them. The names are
// what a task stores and what MainViewModel.CalculateNextDueDate works from, so they must match
// it (and the task screen's list - RecurrencePatternTests checks both). The task screen's list
// ends with one more entry, MonthDayRange.ChoiceName, whose stored form is the range itself
// ("Days 5-12 of each month"), so it is not in All. Nor is "Every 2 years" (RecurrenceInterval),
// which the task screen builds from one of the patterns that count a unit plus a number.
public static class RecurrencePatterns
{
    public static readonly string[] All =
        ["Daily", "Weekday", "Weekly", "Bi-Weekly", "Semi-Monthly", "Monthly", "Bi-Monthly", "Quarterly", "Annually"];

    // The pattern as the list spells it, whatever capitals or spaces were typed (a day range in its
    // stored form); null when it is not one.
    public static string? Find(string? text)
    {
        var typed = (text ?? string.Empty).Trim();
        if (typed.Length == 0) return null;
        return All.FirstOrDefault(p => string.Equals(p, typed, StringComparison.OrdinalIgnoreCase))
            ?? MonthDayRange.Parse(typed)?.ToString()
            ?? RecurrenceInterval.Parse(typed)?.ToString();
    }
}
