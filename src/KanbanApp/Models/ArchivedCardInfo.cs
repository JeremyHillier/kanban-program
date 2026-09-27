namespace KanbanApp.Models;

public class ArchivedCardInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ArchivedAt { get; set; } = string.Empty; // raw stamp; the date-range filter parses it
    public DateTime? CompletedAt { get; set; }

    public string StampsDisplay =>
        $"{(CompletedAt is { } completed ? $"Completed {completed:MMM d, yyyy h:mm tt}" : "Completion date unknown")}" +
        $" • Archived {(DateTime.TryParse(ArchivedAt, out var archived) ? archived.ToString("MMM d, yyyy h:mm tt") : ArchivedAt)}";
}
