namespace KanbanApp.Models;

public class KanbanColumn
{
    public int Id { get; set; }

    // Internal identifier — never changes. Business logic (archiving, completion prompts,
    // recurrence, reminders) matches against this, not the user-facing DisplayName.
    public string Name { get; set; } = string.Empty;

    // What the user sees and can rename via Settings. Defaults to Name.
    public string DisplayName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    // The five columns every task file starts with, in board order. Each has a role the program relies on
    // (Done is where finished tasks go, To Do is where new ones start); only their labels can change.
    public static readonly string[] RoleNames = ["To Do", "In Progress", "On Hold", "Waiting", "Done"];

    // The most columns a board can have: the five above and up to five of the user's own.
    public const int MaxColumns = 10;

    // A column the user added. Its Name is "Custom <Id>" - never reused, and never shown.
    public bool IsCustom => !RoleNames.Contains(Name);
}
