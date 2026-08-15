using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class SubTaskViewModel(SubTaskItem model) : ObservableObject
{
    public SubTaskItem Model { get; } = model;

    public int Id => Model.Id;

    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title == value) return;
            Model.Title = value;
            OnPropertyChanged();
        }
    }

    public bool IsDone
    {
        get => Model.IsDone;
        set
        {
            if (Model.IsDone == value) return;
            Model.IsDone = value;
            OnPropertyChanged();
        }
    }
}
