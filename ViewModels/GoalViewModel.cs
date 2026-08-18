using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class GoalViewModel(Goal model) : ObservableObject, IManagedItem
{
    public Goal Model { get; } = model;

    public int Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value) return;
            Model.Name = value;
            OnPropertyChanged();
        }
    }

    public bool IsActive
    {
        get => Model.IsActive;
        set
        {
            if (Model.IsActive == value) return;
            Model.IsActive = value;
            OnPropertyChanged();
        }
    }
}
