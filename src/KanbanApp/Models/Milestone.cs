namespace KanbanApp.Models;

// Whose milestone it is: a goal's or a project's.
public enum MilestoneOwner { Goal, Project }

// A dated point on the way to a goal or project ("Contract signed", "Beta out"), drawn on the Timeline when it is
// grouped the same way. OwnerId is the goal's or project's id, according to the table it was read from.
public class Milestone
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public MilestoneOwner Owner { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}
