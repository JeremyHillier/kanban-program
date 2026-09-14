using System.Globalization;

namespace KanbanApp.Services;

// Reads and writes the optional due time typed in Add/Edit Task. Stored as "HH:mm" (24-hour).
public static class DueTimeParser
{
    private static readonly string[] Formats =
        ["h:mm tt", "h:mmtt", "h tt", "htt", "H:mm", "HH:mm", "Hmm", "HHmm", "%H"]; // "%H", not "H": a lone letter is read as a standard format and throws

    // Returns the time as "HH:mm", or null if blank or unrecognised.
    // A 1-12 hour typed without AM/PM is ambiguous: preferPm (the AM/PM buttons) settles it, or if
    // neither was clicked, a working-hours guess (7-11 AM, 12-6 PM). A leading zero ("06:00") is
    // read as 24-hour notation and taken literally.
    public static string? Parse(string? text, bool? preferPm)
    {
        var trimmed = (text ?? string.Empty).Replace(".", "").Trim();
        if (trimmed.Length == 0) return null;
        if (trimmed[^1] is 'a' or 'A' or 'p' or 'P') trimmed += "m"; // "2:30p" / "2p"

        var hasMeridiem = trimmed.EndsWith("am", StringComparison.OrdinalIgnoreCase) ||
                          trimmed.EndsWith("pm", StringComparison.OrdinalIgnoreCase);
        var leadingZero = trimmed.Length >= 2 && trimmed[0] == '0' && char.IsDigit(trimmed[1]);
        // "930" - digit-only parsing is greedy and would read "93" as the hour, so pad to "0930".
        if (trimmed.Length == 3 && trimmed.All(char.IsDigit)) trimmed = "0" + trimmed;

        if (!DateTime.TryParseExact(trimmed, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) &&
            !(DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out parsed) &&
              parsed.Date == DateTime.MinValue.Date))
        {
            return null;
        }

        var hour = parsed.Hour;
        if (!hasMeridiem && !leadingZero && hour is >= 1 and <= 12)
        {
            var pm = preferPm ?? hour is 12 or <= 6;
            hour = pm ? (hour == 12 ? 12 : hour + 12) : (hour == 12 ? 0 : hour);
        }

        return $"{hour:00}:{parsed.Minute:00}";
    }

    public static string Format(string? storedTime) =>
        TimeSpan.TryParse(storedTime, out var time) ? DateTime.Today.Add(time).ToString("h:mm tt") : string.Empty;
}
