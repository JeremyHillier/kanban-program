using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// The user's own spell-check words (Services/UserDictionary): kept in the task file, so every PC that
// uses it has them, and given to every spell-checked box. Added from a misspelled word's right-click
// menu, or edited in Settings. A preference, so it is in the Settings snapshot.
public partial class MainViewModel
{
    private const string CustomDictionaryKey = "CustomDictionary";

    public UserDictionary Dictionary { get; } = new();

    // One word a line, as saved.
    public string CustomDictionaryText => Dictionary.AsText;

    private void LoadDictionary()
    {
        // The task file's words plus this PC's, so a word added while another file was open is not lost.
        var saved = _db.GetSetting(CustomDictionaryKey);
        Dictionary.Set(saved + "\n" + Dictionary.ReadPcWords());
        if (!string.Equals(Dictionary.AsText, string.Join("\n", UserDictionary.ParseWords(saved)), StringComparison.Ordinal))
            _db.SetSetting(CustomDictionaryKey, Dictionary.AsText);
        Dictionary.WordsAdded += () => _db.SetSetting(CustomDictionaryKey, Dictionary.AsText);
    }

    // Settings: the whole list at once.
    public void SetCustomDictionary(string? text)
    {
        Dictionary.Set(text);
        _db.SetSetting(CustomDictionaryKey, Dictionary.AsText);
    }
}
