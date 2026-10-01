using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// A task remembers each time it was emailed (Email This Task, from the card or the task screen).
// The app can only know that the email was opened for the user, not that it was sent, so that is
// what the record says.
public partial class MainViewModel
{
    // how: where the email ended up - "in Outlook", "in your email app", "on the clipboard".
    public void RecordCardEmailed(CardViewModel card, string recipients, string how)
    {
        var details = $"to {recipients.Trim()}, {how}";
        _db.RecordCardEmailed(card.Id, card.Title, details);
    }

    public List<CardEmailRecord> GetEmailHistory(CardViewModel card) => _db.GetCardEmailHistory(card.Id);

    // The line on the task screen: the latest time, and how many times in all. Null when never.
    public static string? EmailStampText(IReadOnlyList<CardEmailRecord> history)
    {
        if (history.Count == 0) return null;

        var latest = history[0];
        var text = $"Emailed {latest.When:MMM d, yyyy, h:mm tt} {latest.Details}";
        return history.Count == 1 ? text : $"{text}  ·  {history.Count} times";
    }

    // Every time, newest first, for the line's tooltip.
    public static string EmailHistoryText(IReadOnlyList<CardEmailRecord> history) =>
        string.Join("\n", history.Select(h => $"{h.When:MMM d, yyyy, h:mm tt} {h.Details}"));
}
