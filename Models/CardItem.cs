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
    public int? WhoId { get; set; }
    public DateTime? LastUpdated { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsImported { get; set; }
    public string? Notes { get; set; }
    public List<int> FlagIds { get; set; } = [];
    public List<SubTaskItem> SubTasks { get; set; } = [];
    public List<CardAttachment> Attachments { get; set; } = [];
}
