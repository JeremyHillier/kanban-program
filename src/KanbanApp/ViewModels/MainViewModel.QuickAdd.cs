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

    // The goal Quick Add starts on: whichever it used last time, while that goal is still offered; otherwise none.
    public GoalViewModel? QuickAddDefaultGoal =>
        int.TryParse(_db.GetSetting("QuickAddGoalId"), out var id) ? Goals.FirstOrDefault(g => g.Id == id && g.IsOffered) : null;

    // What the To Do column is called on this board - the user can rename it.
    public string QuickAddColumnName => (Columns.FirstOrDefault(c => c.Name == "To Do") ?? Columns.First()).DisplayName;

    public QuickAddResult ParseQuickAdd(string text) =>
        QuickAddParser.Parse(text, People.Where(p => p.IsActive).Select(p => p.Name), DateTime.Today, Priorities.Names);

    // Null when there is no title left to make a task from.
    public CardViewModel? QuickAdd(string text, ProjectViewModel? project, GoalViewModel? goal = null)
    {
        var parsed = ParseQuickAdd(text);
        if (string.IsNullOrWhiteSpace(parsed.Title)) return null;

        project ??= QuickAddDefaultProject;
        var column = Columns.FirstOrDefault(c => c.Name == "To Do") ?? Columns.First();
        var who = parsed.WhoName is null ? null : People.FirstOrDefault(p => p.Name == parsed.WhoName);

        if (project is not null) _db.SetSetting("QuickAddProjectId", project.Id.ToString());
        _db.SetSetting("QuickAddGoalId", goal is null ? string.Empty : goal.Id.ToString());
        var card = AddCard(parsed.Title, column, project, parsed.Priority ?? Priorities.Default, parsed.DueDate, who, false, null, goal);
        // Jotted down in a hurry: it waits on the review list until the user has looked at it in the full window.
        card.NeedsReview = true;
        _db.SetCardNeedsReview(card.Id, true);
        NotifyQuickReviewChanged();
        return card;
    }
}
