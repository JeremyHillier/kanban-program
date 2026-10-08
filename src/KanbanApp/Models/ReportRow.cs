namespace KanbanApp.Models;

public class ReportRow
{
    public int CardId { get; init; }
    public required string Title { get; init; }
    public required string ColumnName { get; init; }
    // The column's own name ("Done"), which renaming the column on the board doesn't change. Board
    // tasks only; an archived task has left its column.
    public string? ColumnKey { get; init; }
    public required string ProjectName { get; init; }
    public required string Priority { get; init; }
    // Where the priority sits on the task file's list, highest first - for sorting and group order.
    public int PriorityRank { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? StartDate { get; init; }
    public string? WaitingOn { get; init; }
    public string? Who { get; init; } // everyone, for display: "Alice, Bob"
    public List<string> People { get; init; } = []; // the same people one by one, lead first
    public required string GoalName { get; init; }
    public List<string> Flags { get; init; } = [];
    public List<(string Title, bool IsDone)> SubTasks { get; init; } = [];
    public string? Notes { get; init; }
    public bool IsArchived { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
