namespace KanbanApp.Models;

public class CardAttachment
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime AddedDate { get; set; }
}
