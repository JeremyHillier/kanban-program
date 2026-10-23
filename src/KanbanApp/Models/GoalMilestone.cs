namespace KanbanApp.Models;

// A dated point on the way to a goal ("Contract signed", "Beta out"), drawn on the Timeline when it is
// grouped by goal.
public class GoalMilestone
{
    public int Id { get; set; }
    public int GoalId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}
