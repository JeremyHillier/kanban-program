using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public enum PeopleChange { None, Replace, Add, Remove }
public enum DueChange { None, Set, Clear, Shift }
public enum FlagChange { None, Add, Remove }

// What the Mass Update window asks for: only the fields the user ticked are set, so everything else on
// the selected tasks stays exactly as it was.
public sealed class MassUpdateChanges
{
    public bool SetProject { get; init; }
    public ProjectViewModel? Project { get; init; }
    public bool SetGoal { get; init; }
    public GoalViewModel? Goal { get; init; } // null with SetGoal clears the goal
    public PeopleChange People { get; init; }
    public PersonViewModel? Person { get; init; } // null with Replace means Unassigned
    public string? Priority { get; init; }
    public ColumnViewModel? Column { get; init; }
    public DueChange Due { get; init; }
    public DateTime? DueDate { get; init; }
    public int ShiftDays { get; init; }
    public bool SetColor { get; init; }
    public string? Color { get; init; }
    public FlagChange FlagAction { get; init; }
    public FlagViewModel? Flag { get; init; }
    public bool SetWaiting { get; init; }
    public string? WaitingOn { get; init; }

    public IReadOnlyList<string> Lines
    {
        get
        {
            var lines = new List<string>();
            if (SetProject && Project is not null) lines.Add($"Project: {Project.Name}");
            if (SetGoal) lines.Add(Goal is null ? "Goal: none" : $"Goal: {Goal.Name}");
            if (People == PeopleChange.Replace) lines.Add(Person is null ? "Assigned to: nobody" : $"Assigned to: only {Person.Name}");
            if (People == PeopleChange.Add && Person is not null) lines.Add($"Add {Person.Name} to the people");
            if (People == PeopleChange.Remove && Person is not null) lines.Add($"Take {Person.Name} off");
            if (Priority is not null) lines.Add($"Priority: {Priority}");
            if (Column is not null) lines.Add($"Move to {Column.DisplayName}");
            if (Due == DueChange.Set && DueDate is { } due) lines.Add($"Due date: {due:MMM d, yyyy}");
            if (Due == DueChange.Clear) lines.Add("Due date: cleared");
            if (Due == DueChange.Shift) lines.Add($"Due and start dates: {(ShiftDays >= 0 ? "later" : "earlier")} by {Math.Abs(ShiftDays)} day{(Math.Abs(ShiftDays) == 1 ? "" : "s")}");
            if (SetColor) lines.Add(Color is null ? "Colour: none" : $"Colour: {Color}");
            if (FlagAction == FlagChange.Add && Flag is not null) lines.Add($"Add flag {Flag.Name}");
            if (FlagAction == FlagChange.Remove && Flag is not null) lines.Add($"Remove flag {Flag.Name}");
            if (SetWaiting) lines.Add(string.IsNullOrWhiteSpace(WaitingOn) ? "Waiting on: cleared" : $"Waiting on: {WaitingOn!.Trim()}");
            return lines;
        }
    }

    public bool IsEmpty => Lines.Count == 0;
}

// Several tasks changed at once from the Mass Update window. It is one Undo step; if any change fails
// part-way, the step is undone at once so no task is left half-updated.
public partial class MainViewModel
{
    public void SelectAllVisible()
    {
        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.IsVisible)) card.IsSelected = true;
        NotifySelectionChanged();
    }

    // Returns the cards moved to another column, so the board can ask what a move to Done asks.
    public List<CardViewModel> ApplyMassUpdate(IReadOnlyList<CardViewModel> cards, MassUpdateChanges changes)
    {
        List<CardViewModel> moved = [];
        if (cards.Count == 0 || changes.IsEmpty) return moved;

        try
        {
            using var undo = RecordUndo($"Mass update of {cards.Count} task{(cards.Count == 1 ? "" : "s")}", cards);

            if (changes.SetProject && changes.Project is not null) SetCardsProject(cards, changes.Project);
            if (changes.SetGoal) SetCardsGoal(cards, changes.Goal);
            if (changes.Priority is not null) SetCardsPriority(cards, changes.Priority);
            switch (changes.People)
            {
                case PeopleChange.Replace: SetCardsWho(cards, changes.Person); break;
                case PeopleChange.Add when changes.Person is not null: AddPersonToCards(cards, changes.Person); break;
                case PeopleChange.Remove when changes.Person is not null: RemovePersonFromCards(cards, changes.Person); break;
            }

            switch (changes.Due)
            {
                case DueChange.Set: SetCardsDueDate(cards, changes.DueDate); break;
                case DueChange.Clear: SetCardsDueDate(cards, null); break;
                case DueChange.Shift when changes.ShiftDays != 0:
                    ChangeCards("Shift dates of", cards, c => c.DueDate is not null, c =>
                    {
                        c.DueDate = c.DueDate!.Value.AddDays(changes.ShiftDays);
                        if (c.StartDate is { } start) c.StartDate = start.AddDays(changes.ShiftDays);
                    });
                    break;
            }

            if (changes.SetColor) SetCardsColor(cards, changes.Color);
            if (changes.SetWaiting) SetCardsWaitingOn(cards, changes.WaitingOn);
            if (changes.FlagAction == FlagChange.Add && changes.Flag is not null) AddFlagToCards(cards, changes.Flag);
            if (changes.FlagAction == FlagChange.Remove && changes.Flag is not null)
            {
                foreach (var card in cards.Where(c => c.Flags.Any(f => f.Id == changes.Flag.Id)))
                {
                    card.Flags = card.Flags.Where(f => f.Id != changes.Flag.Id).ToList();
                    _db.SetCardFlags(card.Id, card.Flags.Select(f => f.Id));
                }
            }

            // Last, so a task is changed before it is finished.
            if (changes.Column is not null) moved = MoveCards(cards, changes.Column);
        }
        catch
        {
            Undo();
            throw;
        }
        return moved;
    }
}
