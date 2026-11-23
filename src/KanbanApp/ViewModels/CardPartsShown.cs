namespace KanbanApp.ViewModels;

// The details a large card can show and the buttons a card can have, each of which the user can switch off in
// Settings (Large cards show for the details, Card buttons for the buttons). Everything starts on; only what is
// switched off is saved, so a part added later is shown until someone turns it off. Compact cards and narrow
// columns show less anyway, Hide Buttons takes every button off whatever is ticked, and all of it stays on the
// task screen and the right-click menu. New parts go at the end: the saved names are sorted by this order.
public enum CardPart
{
    Goal, Flags, DueDate, AssignedTo, WaitingOn, StartDate, Repeats, Updated, Attachments, SubTasks,
    FlagButton, EmailButton, CalendarButton,
    MoveToDoButton, MoveInProgressButton, MoveOnHoldButton, MoveWaitingButton, DoneButton, DeleteButton
}

public sealed class CardPartsShown
{
    public static readonly CardPartsShown All = new([]);

    private readonly HashSet<CardPart> _hidden;

    private CardPartsShown(IEnumerable<CardPart> hidden) => _hidden = [.. hidden];

    public bool Shows(CardPart part) => !_hidden.Contains(part);

    // One property per part, for the card template to bind to.
    public bool Goal => Shows(CardPart.Goal);
    public bool Flags => Shows(CardPart.Flags);
    public bool DueDate => Shows(CardPart.DueDate);
    public bool AssignedTo => Shows(CardPart.AssignedTo);
    public bool WaitingOn => Shows(CardPart.WaitingOn);
    public bool StartDate => Shows(CardPart.StartDate);
    public bool Repeats => Shows(CardPart.Repeats);
    public bool Updated => Shows(CardPart.Updated);
    public bool Attachments => Shows(CardPart.Attachments);
    public bool SubTasks => Shows(CardPart.SubTasks);
    public bool FlagButton => Shows(CardPart.FlagButton);
    public bool EmailButton => Shows(CardPart.EmailButton);
    public bool CalendarButton => Shows(CardPart.CalendarButton);
    public bool MoveToDoButton => Shows(CardPart.MoveToDoButton);
    public bool MoveInProgressButton => Shows(CardPart.MoveInProgressButton);
    public bool MoveOnHoldButton => Shows(CardPart.MoveOnHoldButton);
    public bool MoveWaitingButton => Shows(CardPart.MoveWaitingButton);
    public bool DoneButton => Shows(CardPart.DoneButton);
    public bool DeleteButton => Shows(CardPart.DeleteButton);

    // The row along the foot of a card is two groups: the four move buttons, then Done and Delete. A group with none
    // of its buttons ticked is left out, and so is the whole row when both are.
    public bool AnyMoveButton => Shows(CardPart.MoveToDoButton) || Shows(CardPart.MoveInProgressButton) || Shows(CardPart.MoveOnHoldButton) || Shows(CardPart.MoveWaitingButton);
    public bool AnyEndButton => Shows(CardPart.DoneButton) || Shows(CardPart.DeleteButton);
    public bool AnyFootButton => AnyMoveButton || AnyEndButton;

    // The buttons, in the order Settings lists them under Card buttons. Every other part is a detail (Large cards show).
    public static readonly IReadOnlyList<CardPart> ButtonParts =
    [
        CardPart.MoveToDoButton, CardPart.MoveInProgressButton, CardPart.MoveOnHoldButton, CardPart.MoveWaitingButton,
        CardPart.DoneButton, CardPart.DeleteButton, CardPart.FlagButton, CardPart.EmailButton, CardPart.CalendarButton
    ];

    public static IEnumerable<CardPart> DetailParts => Enum.GetValues<CardPart>().Except(ButtonParts);

    public CardPartsShown With(CardPart part, bool shown)
    {
        var hidden = new HashSet<CardPart>(_hidden);
        if (shown) hidden.Remove(part); else hidden.Add(part);
        return new CardPartsShown(hidden);
    }

    // "Goal,Updated": the parts switched off, in a fixed order so the same choice always saves the same.
    public string Serialize() => string.Join(",", _hidden.Order());

    // Anything it doesn't recognise (a part from a later version, a typo) is ignored.
    public static CardPartsShown Parse(string? saved) =>
        new((saved ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<CardPart>(name, out var part) ? part : (CardPart?)null)
            .OfType<CardPart>());

    // What Settings calls each one. A move or Done button is named after its column there (it follows a rename);
    // these are the names it starts with.
    public static string Label(CardPart part) => part switch
    {
        CardPart.Goal => "Goal",
        CardPart.Flags => "Flags",
        CardPart.DueDate => "Due date",
        CardPart.AssignedTo => "Assigned to",
        CardPart.WaitingOn => "Waiting on",
        CardPart.StartDate => "Start date",
        CardPart.Repeats => "Repeats",
        CardPart.Updated => "Updated / completed",
        CardPart.Attachments => "Attachments",
        CardPart.SubTasks => "Sub-tasks",
        CardPart.FlagButton => "Flag button",
        CardPart.EmailButton => "Email button",
        CardPart.CalendarButton => "Calendar button",
        CardPart.MoveToDoButton => "Move to To Do",
        CardPart.MoveInProgressButton => "Move to In Progress",
        CardPart.MoveOnHoldButton => "Move to On Hold",
        CardPart.MoveWaitingButton => "Move to Waiting",
        CardPart.DoneButton => "Mark Done",
        CardPart.DeleteButton => "Delete",
        _ => part.ToString()
    };
}
