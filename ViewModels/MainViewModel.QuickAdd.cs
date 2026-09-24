using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// Quick Add: a one-line "new task" box that a system-wide hotkey opens over whatever program is in
// front (MainWindow registers the hotkey; this is the part that makes the task). The task goes into
// To Do with Normal priority unless the line says otherwise - see QuickAddParser for the codes.
public partial class MainViewModel
{
    public bool QuickAddHotkeyEnabled { get; private set; } = true;

    public void SetQuickAddHotkeyEnabled(bool value)
    {
        if (QuickAddHotkeyEnabled == value) return;
        QuickAddHotkeyEnabled = value;
        _db.SetFlag("QuickAddHotkeyEnabled", value);
        OnPropertyChanged(nameof(QuickAddHotkeyEnabled));
    }

    // Set by the window when Windows won't hand over the hotkey (another program has it), so
    // Settings can say so instead of the option just silently not working. Empty when all is well.
    private string _quickAddHotkeyProblem = string.Empty;
    public string QuickAddHotkeyProblem
    {
        get => _quickAddHotkeyProblem;
        set => SetField(ref _quickAddHotkeyProblem, value);
    }

    // The project Quick Add starts on: whichever it used last time, else the first active one.
    // A project is required on every task, so Quick Add always has one chosen.
    public ProjectViewModel? QuickAddDefaultProject =>
        (int.TryParse(_db.GetSetting("QuickAddProjectId"), out var id) ? Projects.FirstOrDefault(p => p.Id == id && p.IsActive) : null)
        ?? Projects.FirstOrDefault(p => p.IsActive)
        ?? Projects.FirstOrDefault();

    public QuickAddResult ParseQuickAdd(string text) =>
        QuickAddParser.Parse(text, People.Where(p => p.IsActive).Select(p => p.Name), DateTime.Today);

    // Null when there is no title left to make a task from.
    public CardViewModel? QuickAdd(string text, ProjectViewModel? project)
    {
        var parsed = ParseQuickAdd(text);
        if (string.IsNullOrWhiteSpace(parsed.Title)) return null;

        project ??= QuickAddDefaultProject;
        var column = Columns.FirstOrDefault(c => c.Name == "To Do") ?? Columns.First();
        var who = parsed.WhoName is null ? null : People.FirstOrDefault(p => p.Name == parsed.WhoName);

        if (project is not null) _db.SetSetting("QuickAddProjectId", project.Id.ToString());
        return AddCard(parsed.Title, column, project, parsed.Priority ?? "Normal", parsed.DueDate, who, false, null, null);
    }
}
