namespace KanbanApp.Models;

public class DeletedCardInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string DeletedAt { get; set; } = string.Empty;
}
