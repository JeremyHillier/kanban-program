using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class PersonViewModel(Person model) : ObservableObject, IManagedItem
{
    public Person Model { get; } = model;
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

    public string? Email
    {
        get => Model.Email;
        set
        {
            if (Model.Email == value) return;
            Model.Email = value;
            OnPropertyChanged();
        }
    }
}
