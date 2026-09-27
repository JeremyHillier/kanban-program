namespace KanbanApp.Models;

public class SubTaskItem
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int SortOrder { get; set; }
}
