using System.Collections.ObjectModel;
using System.Windows.Media;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class ColumnViewModel(KanbanColumn model, Brush background) : ObservableObject
{
    public KanbanColumn Model { get; } = model;

    public Brush Background { get; } = background;

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
}
