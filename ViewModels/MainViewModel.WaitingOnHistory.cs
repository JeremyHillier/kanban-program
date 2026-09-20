using System.Text.Json;

namespace KanbanApp.ViewModels;

// Past Waiting On answers, offered back as suggestions while typing. A task's own Waiting On is
// cleared when it is finished, so the values in use on the board aren't enough by themselves -
// what has been typed before is kept in the task file, most recent first.
public partial class MainViewModel
{
    private const string WaitingOnHistoryKey = "WaitingOnHistory";
    internal const int MaxWaitingOnHistory = 50;

    private List<string>? _waitingOnHistory;

    private List<string> WaitingOnHistory
    {
        get
        {
            if (_waitingOnHistory is not null) return _waitingOnHistory;

            try
            {
                _waitingOnHistory = JsonSerializer.Deserialize<List<string>>(_db.GetSetting(WaitingOnHistoryKey) ?? "[]") ?? [];
            }
            catch (JsonException)
            {
                _waitingOnHistory = [];
            }

            return _waitingOnHistory;
        }
    }

    // Remembered answers first (newest at the top), then anything on a task now that was never
    // typed into this version - an imported task's, say. One entry per answer, whatever its capitals.
    public IReadOnlyList<string> WaitingOnSuggestions =>
        WaitingOnHistory
            .Concat(Columns.SelectMany(c => c.Cards).Select(c => c.WaitingOn).OfType<string>().OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Moves the answer to the top of the list (as typed this time), and drops the oldest past the cap.
    private void RememberWaitingOn(string? value)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrEmpty(cleaned)) return;

        var history = WaitingOnHistory;
        if (history.Count > 0 && history[0] == cleaned) return;

        history.RemoveAll(v => string.Equals(v, cleaned, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, cleaned);
        if (history.Count > MaxWaitingOnHistory) history.RemoveRange(MaxWaitingOnHistory, history.Count - MaxWaitingOnHistory);
        _db.SetSetting(WaitingOnHistoryKey, JsonSerializer.Serialize(history));
    }

    // Takes one answer out of the suggestions, for a typing mistake that keeps coming back. A task
    // that still says it keeps it; it just stops being offered once no task does.
    public void ForgetWaitingOnSuggestion(string value)
    {
        if (WaitingOnHistory.RemoveAll(v => string.Equals(v, value.Trim(), StringComparison.OrdinalIgnoreCase)) == 0) return;
        _db.SetSetting(WaitingOnHistoryKey, JsonSerializer.Serialize(WaitingOnHistory));
    }
}
