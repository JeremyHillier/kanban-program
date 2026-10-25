using System.Globalization;
using System.Text.RegularExpressions;

namespace KanbanApp.Models;

// "Every 2 years": a repeating task that does not come back at every step of its pattern but every
// Nth - Annually every 2 years, Weekly every 3 weeks, Monthly every 6 months. Stored in
// Cards.RecurrencePattern as readable text ("Every 2 years"), like "Days 5-12 of each month", so
// emails, Excel files, reports and templates carry it as is. Every 1 is just the plain pattern
// (Weekly), so it is never stored this way.
//
// Only the patterns that count one unit can be repeated every N: Daily, Weekly, Monthly, Quarterly
// and Annually. Bi-Weekly and Bi-Monthly are already "every 2", Weekday and Semi-Monthly are not a
// number of anything, and Days of month has its own line.
public sealed partial record RecurrenceInterval(int Count, RecurrenceInterval.Unit Of)
{
    public enum Unit { Day, Week, Month, Quarter, Year }

    public const int Largest = 99;

    // The patterns of the task screen's Repeats list that take a count, in the order of Unit.
    public static readonly string[] Patterns = ["Daily", "Weekly", "Monthly", "Quarterly", "Annually"];

    public static bool Takes(string? pattern) => pattern is not null && Patterns.Contains(pattern, StringComparer.Ordinal);

    // The pattern a unit belongs to ("Annually" for years), and back.
    public string Pattern => Patterns[(int)Of];

    public static Unit UnitOf(string pattern) => (Unit)Array.IndexOf(Patterns, pattern);

    public static string UnitName(Unit unit, int count) =>
        unit.ToString().ToLowerInvariant() + (count == 1 ? string.Empty : "s");

    // What a task stores: the plain pattern for 1 (or none), "Every N units" for 2 and up.
    public static string Compose(string pattern, int count) =>
        Takes(pattern) && count >= 2 ? new RecurrenceInterval(count, UnitOf(pattern)).ToString() : pattern;

    public override string ToString() => $"Every {Count.ToString(CultureInfo.InvariantCulture)} {UnitName(Of, Count)}";

    [GeneratedRegex(@"^\s*every\s+(\d{1,2})\s+(days?|weeks?|months?|quarters?|years?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern_();

    // Null for anything that isn't one, and for a count outside 2 to 99 ("Every 1 week" is just Weekly).
    public static RecurrenceInterval? Parse(string? text)
    {
        var match = Pattern_().Match(text ?? string.Empty);
        if (!match.Success) return null;

        var count = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        if (count is < 2 or > Largest) return null;

        var unit = match.Groups[2].Value.TrimEnd('s', 'S').ToLowerInvariant() switch
        {
            "day" => Unit.Day, "week" => Unit.Week, "month" => Unit.Month, "quarter" => Unit.Quarter, _ => Unit.Year
        };
        return new RecurrenceInterval(count, unit);
    }

    // The date after anchor, Count steps on.
    public DateTime Next(DateTime anchor) => Of switch
    {
        Unit.Day => anchor.AddDays(Count),
        Unit.Week => anchor.AddDays(7 * Count),
        Unit.Month => anchor.AddMonths(Count),
        Unit.Quarter => anchor.AddMonths(3 * Count),
        _ => anchor.AddYears(Count)
    };

    // How a repeating task reads on a card or in an email: "every 2 years" in the middle of a sentence.
    public static string Phrase(string? pattern) =>
        Parse(pattern) is { } interval ? "every" + interval.ToString()[5..] : pattern ?? string.Empty;
}
