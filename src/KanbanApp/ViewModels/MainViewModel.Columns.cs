using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// The five columns by role, for the move buttons on each card, and the names the user can give
// them. A column's role never changes (Done is always where finished tasks go); only its label does.
public partial class MainViewModel
{
    public ColumnViewModel? ToDoColumn => ColumnNamed("To Do");
    public ColumnViewModel? InProgressColumn => ColumnNamed("In Progress");
    public ColumnViewModel? OnHoldColumn => ColumnNamed("On Hold");
    public ColumnViewModel? WaitingColumn => ColumnNamed("Waiting");
    public ColumnViewModel? DoneColumn => ColumnNamed("Done");

    private ColumnViewModel? ColumnNamed(string name) => Columns.FirstOrDefault(c => c.Name == name);

    // Column labels, in board order. Standard is what a new task file starts with; the other set
    // suits people who work the Getting Things Done way.
    public static readonly string[] StandardColumnNames = ["To Do", "In Progress", "On Hold", "Waiting", "Done"];
    public static readonly string[] GtdColumnNames = ["Inbox", "Next Actions", "In Progress", "Waiting", "Done"];

    public void UseColumnNames(IReadOnlyList<string> names)
    {
        for (var i = 0; i < Columns.Count && i < names.Count; i++) RenameColumnDisplayName(Columns[i], names[i]);
    }

    // Every column but Done gets a letter from its current name; Done's button is always a tick.
    private void RefreshColumnLetters()
    {
        var lettered = Columns.Where(c => c.Name != "Done").ToList();
        var letters = ColumnLetters.Assign(lettered.Select(c => c.DisplayName).ToList());
        for (var i = 0; i < lettered.Count; i++) lettered[i].QuickLetter = letters[i];
    }
}
