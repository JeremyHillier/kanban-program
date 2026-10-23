namespace KanbanApp.Models;

public class Project : ManagedListEntry
{
    // Optional dates for the project, like a goal's: when work starts and when it should be finished. The Timeline,
    // grouped by project, draws them.
    public DateTime? StartDate { get; set; }
    public DateTime? TargetDate { get; set; }
}
