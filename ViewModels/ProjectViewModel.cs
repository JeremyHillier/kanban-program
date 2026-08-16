using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class ProjectViewModel(Project model) : ObservableObject
{
    public Project Model { get; } = model;

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
