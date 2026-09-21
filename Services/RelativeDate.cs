using System.Globalization;

namespace KanbanApp.Services;

// A saved report's date can be a fixed day ("2026-09-21") or a day relative to when the report is
// run: "today", "today+7", "today-30". A fixed date in a saved view goes stale within days, which
// is why the relative form exists; the Report Builder's Today buttons save the relative form.
public static class RelativeDate
{
    public const string Today = "today";

    public static bool IsRelative(string? saved) => saved is not null && saved.Trim().StartsWith(Today, StringComparison.OrdinalIgnoreCase);

    // "today+7" for 7, "today-3" for -3, "today" for 0.
    public static string Relative(int daysFromToday) => daysFromToday == 0 ? Today : $"{Today}{daysFromToday:+0;-0}";

    // The day the saved text stands for on the given date; null when there is nothing there, or
    // it can't be read.
    public static DateTime? Resolve(string? saved, DateTime today)
    {
        if (string.IsNullOrWhiteSpace(saved)) return null;
        var text = saved.Trim();

        if (text.StartsWith(Today, StringComparison.OrdinalIgnoreCase))
        {
            var rest = text[Today.Length..].Replace(" ", "");
            if (rest.Length == 0) return today.Date;
            return int.TryParse(rest, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var days) && Math.Abs(days) <= 3650 ? today.Date.AddDays(days) : null;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fixedDate) ? fixedDate.Date : null;
    }

    // How to say it to the user: "today", "today + 7 days", "Sep 21, 2026".
    public static string Describe(string? saved) =>
        saved is null || saved.Trim().Length == 0 ? "any"
        : !IsRelative(saved) ? (Resolve(saved, DateTime.Today)?.ToString("MMM d, yyyy") ?? "any")
        : Resolve(saved, DateTime.Today) is null ? "any"
        : saved.Trim().Length == Today.Length ? "today"
        : $"today {(saved.Contains('-') ? "-" : "+")} {Math.Abs(int.Parse(saved.Trim()[Today.Length..].Replace(" ", ""), CultureInfo.InvariantCulture))} day{(Math.Abs(int.Parse(saved.Trim()[Today.Length..].Replace(" ", ""), CultureInfo.InvariantCulture)) == 1 ? "" : "s")}";
}
