namespace KanbanApp.Models;

public class ImportedTaskRow
{
    public required string Title { get; init; }
    public string? Category { get; init; }
    public string? Priority { get; init; }
    public string? Project { get; init; }
    public string? Goal { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Who { get; init; }
}
