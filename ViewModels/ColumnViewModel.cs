using System.Collections.ObjectModel;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class ColumnViewModel(KanbanColumn model) : ObservableObject
{
    public KanbanColumn Model { get; } = model;

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

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    private string _newCardTitle = string.Empty;
    public string NewCardTitle
    {
        get => _newCardTitle;
        set => SetField(ref _newCardTitle, value);
    }
}
