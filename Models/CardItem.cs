namespace KanbanApp.Models;

public class CardItem
{
    public int Id { get; set; }
    public int ColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? ProjectId { get; set; }
    public int? GoalId { get; set; }
    public bool IsArchived { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTime? DueDate { get; set; }
    public string? Who { get; set; }
    public DateTime? LastUpdated { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
}
