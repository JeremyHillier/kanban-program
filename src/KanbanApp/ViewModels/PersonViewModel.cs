using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class PersonViewModel(Person model) : ManagedListEntryViewModel<Person>(model)
{
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
