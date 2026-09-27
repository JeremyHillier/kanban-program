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
}
