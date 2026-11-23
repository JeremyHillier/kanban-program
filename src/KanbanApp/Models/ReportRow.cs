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
    public bool SubTasksPickOne { get; init; } // the sub-tasks are choices, the done one picked
    public string? Notes { get; init; }
    // When the task was last emailed and to whom, with how many times in all; null for a task never emailed.
    public string? Emailed { get; init; }
    // The custom fields chosen for the report that this task holds a value in, named as the report shows them.
    public List<(string Name, string Value)> CustomFields { get; init; } = [];
    public bool IsArchived { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    // Set once the rows are picked (ReportService.BuildRows): where the due date falls (Overdue, Today, This Week,
    // Later, No Due Date), for grouping by due date, and whether the report lists the short form of each task.
    public string DueBucket { get; set; } = string.Empty;
    public bool Brief { get; set; }
}
