using System.Collections.ObjectModel;
using System.Windows.Media;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class ColumnViewModel(KanbanColumn model, Brush background) : ObservableObject
{
    public KanbanColumn Model { get; } = model;

    private Brush _background = background;
    public Brush Background
    {
        get => _background;
        set => SetField(ref _background, value);
    }

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

    public string DisplayName
    {
        get => Model.DisplayName;
        set
        {
            if (Model.DisplayName == value) return;
            Model.DisplayName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    // Manual-sort drag-reorder insertion indicator (see MainWindow.xaml.cs's CardsScrollViewer_*
    // handlers) - bound directly rather than named-element lookup, since this DataTemplate repeats
    // once per column.
    private bool _isDropIndicatorVisible;
    public bool IsDropIndicatorVisible
    {
        get => _isDropIndicatorVisible;
        set => SetField(ref _isDropIndicatorVisible, value);
    }

    private double _dropIndicatorY;
    public double DropIndicatorY
    {
        get => _dropIndicatorY;
        set => SetField(ref _dropIndicatorY, value);
    }
}
