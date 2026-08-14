namespace KanbanApp.Models;

public class CardItem
{
    public int Id { get; set; }
    public int ColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? ProjectId { get; set; }
}
