namespace KanbanApp.Models;

public class Goal : ManagedListEntry
{
    // When the goal should be finished (optional). The Timeline marks it and flags a goal whose open
    // tasks run past it.
    public DateTime? TargetDate { get; set; }
}
