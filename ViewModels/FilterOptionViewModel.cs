namespace KanbanApp.ViewModels;

// One selectable name in a multi-select filter list (Project/Priority/Who, on both the board and
// Report Builder). Backs a ListBox's ItemContainerStyle, which binds ListBoxItem.IsSelected to this
// TwoWay - Ctrl/Shift-click multi-select then falls out of the ListBox's own built-in behavior with
// no extra code, and IsSelected here stays the single source of truth in both directions.
public class FilterOptionViewModel(string name) : ObservableObject
{
    public string Name { get; } = name;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
