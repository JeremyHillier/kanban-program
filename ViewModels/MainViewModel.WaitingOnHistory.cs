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

// Managing the list by hand (the Manage Waiting On List screen).
public partial class MainViewModel
{
    // One line of the managed list: an answer, and how many tasks on the board say it right now.
    public sealed record WaitingOnEntry(string Text, int TaskCount)
    {
        public string Display => TaskCount == 0 ? Text : $"{Text}   ({TaskCount} task{(TaskCount == 1 ? "" : "s")})";
    }

    // Alphabetical here, for finding things; the order answers are suggested in stays most-recent-first.
    public List<WaitingOnEntry> WaitingOnEntries =>
        WaitingOnSuggestions
            .OrderBy(text => text, StringComparer.OrdinalIgnoreCase)
            .Select(text => new WaitingOnEntry(text, CardsWaitingOn(text).Count))
            .ToList();

    private List<CardViewModel> CardsWaitingOn(string text) =>
        Columns.SelectMany(c => c.Cards).Where(c => string.Equals(c.WaitingOn, text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    // False when it is blank or already on the list.
    public bool AddWaitingOnSuggestion(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.Length == 0 || WaitingOnSuggestions.Contains(cleaned, StringComparer.OrdinalIgnoreCase)) return false;

        RememberWaitingOn(cleaned);
        return true;
    }

    // Changes the wording on the list and on every task that says it, as one Undo step for the
    // tasks. Renaming onto an answer that already exists merges the two. Returns how many tasks
    // changed, or -1 when there was nothing to do (blank, or the same text).
    public int RenameWaitingOnSuggestion(string oldText, string newText)
    {
        var from = oldText.Trim();
        var to = newText.Trim();
        if (to.Length == 0 || from == to) return -1;

        var history = WaitingOnHistory;
        var index = history.FindIndex(v => string.Equals(v, from, StringComparison.OrdinalIgnoreCase));
        history.RemoveAll(v => string.Equals(v, from, StringComparison.OrdinalIgnoreCase) || string.Equals(v, to, StringComparison.OrdinalIgnoreCase));
        history.Insert(Math.Clamp(index, 0, history.Count), to); // keeps its place in the suggestion order
        _db.SetSetting(WaitingOnHistoryKey, JsonSerializer.Serialize(history));

        var cards = CardsWaitingOn(from);
        ChangeCards("Reword waiting-on of", cards, c => c.WaitingOn != to, c => c.WaitingOn = to);
        return cards.Count;
    }

    // Takes the answer off the list. With clearFromTasks, tasks that say it stop waiting too (one
    // Undo step); without, they keep it, and it goes on being suggested until none of them does.
    public int DeleteWaitingOnSuggestion(string text, bool clearFromTasks)
    {
        ForgetWaitingOnSuggestion(text);
        if (!clearFromTasks) return 0;

        var cards = CardsWaitingOn(text);
        SetCardsWaitingOn(cards, null);
        return cards.Count;
    }
}
