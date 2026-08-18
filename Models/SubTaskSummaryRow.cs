namespace KanbanApp.Models;

public class SubTaskSummaryRow
{
    public required string ParentTitle { get; init; }
    public required string SubTaskTitle { get; init; }
    public int CompletedCount { get; init; }
    public int TotalCount { get; init; }
}
