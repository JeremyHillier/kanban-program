namespace KanbanApp.Models;

public class Goal : ManagedListEntry
{
    // When the goal should be finished (optional). The Timeline marks it and flags a goal whose open
    // tasks run past it.
    public DateTime? TargetDate { get; set; }

    // When work on the goal begins (optional). The Timeline bar starts here when it is earlier than its first task.
    public DateTime? StartDate { get; set; }

    // The day the goal was marked complete (null while it is still being worked on). A complete goal is no longer
    // offered for new tasks or the board's filter, and the Timeline hides it unless Include Done tasks is ticked.
    public DateTime? CompletedDate { get; set; }
}
