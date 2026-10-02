namespace KanbanApp.Models;

// One task as an Excel row: the import template, a customer's own spreadsheet, or the one-task file
// attached to an email. Everything but Title is optional.
public class ImportedTaskRow
{
    public required string Title { get; init; }
    public string? Category { get; init; }
    public string? Priority { get; init; }
    public string? Project { get; init; }
    public string? Goal { get; init; }
    public DateTime? DueDate { get; init; }
    public string? DueTime { get; init; }            // "HH:mm", like the task stores it
    public DateTime? StartDate { get; init; }
    public string? WaitingOn { get; init; }
    public string? Who { get; init; }                // several people separated by semicolons; the first is the lead
    public string? RecurrencePattern { get; init; }  // one of RecurrencePatterns.All, or null for a one-off task
    public int? RecurrenceCount { get; init; }       // times it still happens counting this one; null = no end
    public string? Flags { get; init; }              // separated by semicolons
    public string? WebsiteUrl { get; init; }
    public string? Notes { get; init; }
    public List<(string Title, bool IsDone)> SubTasks { get; init; } = [];
}
