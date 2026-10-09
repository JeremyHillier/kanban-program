using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// A task remembers each time it was emailed (Email This Task) and each time it was put in a calendar
// (Schedule in Outlook), from the card or the task screen. The app can only know that the email or
// appointment was opened for the user, not that it was sent or saved, so that is what the record says.
public partial class MainViewModel
{
    // how: where the email ended up - "in Outlook", "in your email app", "on the clipboard".
    public void RecordCardEmailed(CardViewModel card, string recipients, string how)
    {
        var details = $"to {recipients.Trim()}, {how}";
        _db.RecordCardEmailed(card.Id, card.Title, details);
    }

    public List<CardEmailRecord> GetEmailHistory(CardViewModel card) => _db.GetCardEmailHistory(card.Id);

    // The line on the task screen: the latest time and recipients, and how many times in all. Null
    // when never. The "how" is left to the tooltip (EmailHistoryText), to keep the line short.
    public static string? EmailStampText(IReadOnlyList<CardEmailRecord> history)
    {
        if (history.Count == 0) return null;

        var latest = history[0];
        var text = $"Emailed {latest.When:MMM d, yyyy, h:mm tt} {WithoutHow(latest.Details)}";
        return history.Count == 1 ? text : $"{text} ({history.Count} times)";
    }

    // when: "for Oct 12, 2026 at 2:30 PM" (CalendarFile.Describe); how: "in Outlook" / "in your calendar app".
    public void RecordCardScheduled(CardViewModel card, string when, string how) =>
        _db.RecordCardScheduled(card.Id, card.Title, $"{when}, {how}");

    public List<CardEmailRecord> GetScheduleHistory(CardViewModel card) => _db.GetCardScheduleHistory(card.Id);

    // "Scheduled Oct 9, 2026, 10:15 AM for Oct 12, 2026 at 2:30 PM" - the latest; null when never.
    public static string? ScheduleStampText(IReadOnlyList<CardEmailRecord> history)
    {
        if (history.Count == 0) return null;

        var latest = history[0];
        var text = $"Scheduled {latest.When:MMM d, yyyy, h:mm tt} {WithoutHow(latest.Details)}";
        return history.Count == 1 ? text : $"{text} ({history.Count} times)";
    }

    // "to sam@x.com, in Outlook" -> "to sam@x.com"; "for Oct 12, 2026, all day, in Outlook" -> "for Oct 12, 2026, all day".
    private static string WithoutHow(string details)
    {
        var comma = details.LastIndexOf(", ", StringComparison.Ordinal);
        return comma > 0 ? details[..comma] : details;
    }

    // Every time, newest first, for the line's tooltip (either kind).
    public static string EmailHistoryText(IReadOnlyList<CardEmailRecord> history) =>
        string.Join("\n", history.Select(h => $"{h.When:MMM d, yyyy, h:mm tt} {h.Details}"));
}
