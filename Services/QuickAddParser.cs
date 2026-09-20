using System.Globalization;

namespace KanbanApp.Services;

// Reads the one line typed into Quick Add. Most of it is the task's title; a few short codes, each
// its own word, set the things worth setting in a hurry:
//
//   !high !medium !normal !low   (or !h !m !n !l)      priority
//   @sam                                               who - the start of a person's name, if it
//                                                      matches exactly one person
//   /today /tomorrow /fri /friday /+3 /10-15 /2026-10-15   due date
//
// Anything that doesn't read as one of those stays in the title exactly as typed, so an email
// address, "and/or", or "50/50" are safe. The last code of each kind wins.
public sealed record QuickAddResult(string Title, string? Priority, string? WhoName, DateTime? DueDate);

public static class QuickAddParser
{
    public const string Hint = "!high   @name   /tomorrow  /fri  /+3  /10-15";

    private static readonly Dictionary<string, string> Priorities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["!high"] = "High", ["!h"] = "High", ["!medium"] = "Medium", ["!med"] = "Medium", ["!m"] = "Medium",
        ["!normal"] = "Normal", ["!n"] = "Normal", ["!low"] = "Low", ["!l"] = "Low"
    };

    public static QuickAddResult Parse(string text, IEnumerable<string> peopleNames, DateTime today)
    {
        var people = peopleNames.ToList();
        var titleWords = new List<string>();
        string? priority = null, who = null;
        DateTime? due = null;

        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Priorities.TryGetValue(word, out var p)) priority = p;
            else if (word.Length > 1 && word[0] == '@' && MatchPerson(word[1..], people) is { } person) who = person;
            else if (word.Length > 1 && word[0] == '/' && ParseDate(word[1..], today.Date) is { } date) due = date;
            else titleWords.Add(word);
        }

        return new QuickAddResult(string.Join(' ', titleWords), priority, who, due);
    }

    // The whole name with its spaces taken out ("@samlee"), or else the start of a name - but only
    // when that points at one person. "@s" with both Sam and Sara on the list matches nobody.
    private static string? MatchPerson(string typed, List<string> people)
    {
        var exact = people.Where(n => string.Equals(n.Replace(" ", ""), typed, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1) return exact[0];

        var starts = people.Where(n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase)).ToList();
        return starts.Count == 1 ? starts[0] : null;
    }

    private static DateTime? ParseDate(string typed, DateTime today)
    {
        if (typed.Equals("today", StringComparison.OrdinalIgnoreCase)) return today;
        if (typed.Equals("tomorrow", StringComparison.OrdinalIgnoreCase) || typed.Equals("tom", StringComparison.OrdinalIgnoreCase)) return today.AddDays(1);

        if (typed[0] == '+' && int.TryParse(typed[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days <= 3650) return today.AddDays(days);

        // A day name means the next one to come, never today: "/fri" typed on a Friday is a week away.
        if (typed.Length >= 3)
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                var name = day.ToString();
                if (!name.Equals(typed, StringComparison.OrdinalIgnoreCase) && !name[..3].Equals(typed, StringComparison.OrdinalIgnoreCase)) continue;
                var ahead = ((int)day - (int)today.DayOfWeek + 7) % 7;
                return today.AddDays(ahead == 0 ? 7 : ahead);
            }
        }

        if (DateTime.TryParseExact(typed, "yyyy-M-d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var full)) return full;

        // Month-day with no year: this year, or next year if that date has already gone by.
        var parts = typed.Split('-');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var dayOfMonth)
            && month is >= 1 and <= 12 && dayOfMonth >= 1)
        {
            foreach (var year in new[] { today.Year, today.Year + 1 })
            {
                if (dayOfMonth > DateTime.DaysInMonth(year, month)) continue;
                var candidate = new DateTime(year, month, dayOfMonth);
                if (candidate >= today) return candidate;
            }
        }

        return null;
    }
}
