using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// A task's history for the task screen: a one-line "Last change" and, on hover, the whole list, newest first.
public partial class MainViewModel
{
    private const int HistoryTooltipLimit = 40;

    public List<CardHistoryRecord> GetTaskHistory(CardViewModel card) => _db.GetCardHistory(card.Id);

    // One entry per save: the several rows a single save writes (details, people, flags, sub-tasks) are joined.
    // Newest first.
    internal static List<(DateTime When, string Text, string? Computer)> HistoryEntries(IReadOnlyList<CardHistoryRecord> newestFirst)
    {
        var entries = new List<(DateTime When, string Text, string? Computer, bool Edit)>();
        foreach (var row in newestFirst.Reverse())
        {
            var text = row.Event switch
            {
                "Created" or "Edited" or "Moved" => row.Details,
                "Emailed" or "Scheduled" => $"{row.Event} {row.Details}",
                _ => string.IsNullOrWhiteSpace(row.Details) ? row.Event : $"{row.Event}: {row.Details}"
            };
            var edit = row.Event == "Edited";

            if (edit && entries.Count > 0 && entries[^1] is { Edit: true } last && last.Computer == row.Computer
                && last.When.Date == row.When.Date && last.When.Hour == row.When.Hour && last.When.Minute == row.When.Minute)
            {
                entries[^1] = (last.When, last.Text + " · " + text, last.Computer, true);
                continue;
            }
            entries.Add((row.When, text, row.Computer, edit));
        }

        entries.Reverse();
        return entries.Select(e => (e.When, e.Text, e.Computer)).ToList();
    }

    private static string HistoryLine((DateTime When, string Text, string? Computer) e) =>
        $"{e.When:MMM d, yyyy, h:mm tt}  {e.Text}" + (string.IsNullOrEmpty(e.Computer) ? "" : $"  ({e.Computer})");

    // "Last change: Nov 9, 2026, 2:14 PM  Due date Nov 3, 2026 -> Nov 10, 2026"; null when there is no history.
    public static string? HistoryStampText(IReadOnlyList<CardHistoryRecord> newestFirst)
    {
        var entries = HistoryEntries(newestFirst);
        if (entries.Count == 0) return null;

        var latest = entries[0];
        return $"History: {latest.When:MMM d, yyyy, h:mm tt}  {latest.Text}";
    }

    // The whole history for the line's tooltip, newest first (the latest 40, then a count of the rest).
    public static string HistoryTooltipText(IReadOnlyList<CardHistoryRecord> newestFirst)
    {
        var entries = HistoryEntries(newestFirst);
        var lines = entries.Take(HistoryTooltipLimit).Select(HistoryLine).ToList();
        if (entries.Count > HistoryTooltipLimit) lines.Add($"... and {entries.Count - HistoryTooltipLimit} earlier");
        return "History of this task\n" + string.Join("\n", lines);
    }
}
