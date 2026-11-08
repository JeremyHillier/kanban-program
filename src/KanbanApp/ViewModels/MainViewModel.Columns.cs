using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// The columns. Five have a role (the move buttons on each card use four of them) and the names the user can give
// them: a column's role never changes (Done is always where finished tasks go), only its label does. The user can
// also add columns of their own, up to MaxColumns in all. An added column has no role and no move button: it is a
// place to put tasks, before Done, that can be moved along and removed again.
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

    public const int MaxColumns = KanbanColumn.MaxColumns;

    public bool CanAddColumn => Columns.Count < MaxColumns;

    // Names the five standard columns, by role (the columns the user added are left alone).
    public void UseColumnNames(IReadOnlyList<string> names)
    {
        for (var i = 0; i < names.Count && i < KanbanColumn.RoleNames.Length; i++)
        {
            if (ColumnNamed(KanbanColumn.RoleNames[i]) is { } column) RenameColumnDisplayName(column, names[i]);
        }
    }

    // Every standard column but Done gets a letter from its current name; Done's button is always a tick. A column
    // the user added has no move button, so no letter, and does not take one from the others.
    private void RefreshColumnLetters()
    {
        var lettered = Columns.Where(c => c.Name != "Done" && !c.IsCustom).ToList();
        var letters = ColumnLetters.Assign(lettered.Select(c => c.DisplayName).ToList());
        for (var i = 0; i < lettered.Count; i++) lettered[i].QuickLetter = letters[i];
        foreach (var column in Columns.Where(c => c.IsCustom)) column.QuickLetter = string.Empty;
    }

    // Adds a column of the user's own, just before Done. Returns what is wrong, or null when it was added.
    public string? AddColumn(string name)
    {
        var label = name.Trim();
        if (label.Length == 0) return "Type a name for the new column.";
        if (!CanAddColumn) return $"A board has {MaxColumns} columns at most.";
        if (Columns.Any(c => string.Equals(c.DisplayName, label, StringComparison.OrdinalIgnoreCase))) return $"There is already a column called \"{label}\".";

        var column = new ColumnViewModel(_db.AddCustomColumn(label), System.Windows.Media.Brushes.Transparent);
        var done = ColumnNamed("Done");
        Columns.Insert(done is null ? Columns.Count : Columns.IndexOf(done), column);
        ColumnsChanged();
        IncludeInSavedReports(column);
        return null;
    }

    // Moves a column left (-1) or right (+1). Done stays last, and the first column stays first: it is where new tasks go.
    public bool CanMoveColumn(ColumnViewModel column, int direction)
    {
        var index = Columns.IndexOf(column);
        var target = index + direction;
        return column.IsCustom && index >= 0 && target >= 1 && target < Columns.Count && Columns[target].Name != "Done";
    }

    public void MoveColumn(ColumnViewModel column, int direction)
    {
        if (!CanMoveColumn(column, direction)) return;
        Columns.Move(Columns.IndexOf(column), Columns.IndexOf(column) + direction);
        _db.SetColumnOrder(Columns.Select(c => c.Id).ToList());
        ColumnsChanged();
    }

    // How many tasks a column holds, archived and deleted ones too: they all move if it is removed.
    public int TasksInColumn(ColumnViewModel column) => _db.CountCardsInColumn(column.Id);

    // Removes a column the user added; its tasks go to the end of To Do.
    public void RemoveColumn(ColumnViewModel column)
    {
        if (!column.IsCustom) return;
        var target = ToDoColumn ?? Columns.FirstOrDefault(c => c != column);
        if (target is null) return;

        _db.DeleteColumn(column.Id, target.Id);
        foreach (var card in column.Cards.ToList())
        {
            column.Cards.Remove(card);
            card.ColumnId = target.Id;
            target.Cards.Add(card);
        }

        Columns.Remove(column);
        ClearUndoHistory(); // its saved steps may point at the column that has gone
        ApplySort();
        ColumnsChanged();
        RefreshDashboardStats();
    }

    // Everything that depends on which columns there are, and in what order.
    private void ColumnsChanged()
    {
        ApplyColumnPalette();
        NotifyColumnWidthChanged(); // the columns share the board's width
        RefreshColumnLetters();
        OnPropertyChanged(nameof(CanAddColumn));
        if (IsSwimlanesOn) RebuildLanes(); // each lane has every column across it
    }

    // A saved report that already takes every other open column takes the new one too, so a task put in it is
    // not left out of the reports that cover the rest of the board.
    private void IncludeInSavedReports(ColumnViewModel added)
    {
        var others = Columns.Where(c => c != added && c.Name != "Done").Select(c => c.Name).ToList();
        var changed = false;
        foreach (var view in SavedReportViews)
        {
            if (view.IncludedColumns.Contains(added.Name) || !others.All(view.IncludedColumns.Contains)) continue;
            view.IncludedColumns.Add(added.Name);
            changed = true;
        }
        if (changed) PersistSavedReportViews();
    }
}
