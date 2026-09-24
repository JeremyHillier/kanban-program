namespace KanbanApp.Models;

// What every entry in a managed list has - a project, goal, flag or person. People add an email.
public abstract class ManagedListEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
