namespace KanbanApp.ViewModels;

public interface IManagedItem
{
    int Id { get; }
    string Name { get; set; }
    bool IsActive { get; set; }
}
