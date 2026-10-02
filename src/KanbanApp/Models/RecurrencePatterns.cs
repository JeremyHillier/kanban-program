namespace KanbanApp.Models;

// The ways a recurring task can repeat, in the order the task screen lists them. The names are
// what a task stores and what MainViewModel.CalculateNextDueDate works from, so they must match
// it (and the task screen's list - RecurrencePatternTests checks both).
public static class RecurrencePatterns
{
    public static readonly string[] All =
        ["Daily", "Weekday", "Weekly", "Bi-Weekly", "Semi-Monthly", "Monthly", "Bi-Monthly", "Quarterly", "Annually"];

    // The pattern as the list spells it, whatever capitals or spaces were typed; null when it is not one.
    public static string? Find(string? text)
    {
        var typed = (text ?? string.Empty).Trim();
        return typed.Length == 0 ? null : All.FirstOrDefault(p => string.Equals(p, typed, StringComparison.OrdinalIgnoreCase));
    }
}
