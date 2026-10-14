using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// Auto-correct in the task box and Notes: on or off, and the user's own corrections ("wrong = right",
// one per line) on top of the built-in list. Preferences, so both are in the Settings snapshot.
public partial class MainViewModel
{
    public bool AutoCorrectEnabled { get; private set; } = true;

    public string AutoCorrectEntries { get; private set; } = string.Empty;

    private IReadOnlyDictionary<string, string> _autoCorrectOwn = new Dictionary<string, string>();

    // What the text boxes look up on every finished word.
    public IReadOnlyDictionary<string, string> AutoCorrectOwn => _autoCorrectOwn;

    private void LoadAutoCorrect()
    {
        AutoCorrectEnabled = _db.GetFlag("AutoCorrect", true);
        AutoCorrectEntries = _db.GetSetting("AutoCorrectEntries") ?? string.Empty;
        _autoCorrectOwn = AutoCorrector.ParseEntries(AutoCorrectEntries);
    }

    public void SetAutoCorrectEnabled(bool value)
    {
        AutoCorrectEnabled = value;
        _db.SetFlag("AutoCorrect", value);
    }

    public void SetAutoCorrectEntries(string? text)
    {
        AutoCorrectEntries = text ?? string.Empty;
        _autoCorrectOwn = AutoCorrector.ParseEntries(AutoCorrectEntries);
        _db.SetSetting("AutoCorrectEntries", AutoCorrectEntries);
    }
}
