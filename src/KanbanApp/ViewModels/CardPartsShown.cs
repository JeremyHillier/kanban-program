namespace KanbanApp.ViewModels;

// The details and buttons a large card can show, each of which the user can switch off in Settings
// (Large cards show). Everything starts on; only what is switched off is saved, so a part added
// later is shown until someone turns it off. Compact cards and narrow columns show less anyway, and
// all of it stays on the task screen and the right-click menu.
public enum CardPart { Goal, Flags, DueDate, AssignedTo, WaitingOn, StartDate, Repeats, Updated, Attachments, SubTasks, FlagButton, EmailButton, CalendarButton }

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

    // What Settings calls each one.
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
        _ => part.ToString()
    };
}
