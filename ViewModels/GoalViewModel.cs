using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class GoalViewModel(Goal model) : ObservableObject
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
}
