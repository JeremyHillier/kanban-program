namespace KanbanApp.Models;

public class ReportRow
{
    public required string Title { get; init; }
    public required string ColumnName { get; init; }
    public required string ProjectName { get; init; }
    public required string Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Who { get; init; }
    public required string GoalName { get; init; }
    public List<string> Flags { get; init; } = [];
    public List<(string Title, bool IsDone)> SubTasks { get; init; } = [];
    public string? Notes { get; init; }
    public bool IsArchived { get; init; }
    public DateTime? ArchivedAt { get; init; }
}
