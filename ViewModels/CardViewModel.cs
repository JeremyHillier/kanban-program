using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class CardViewModel(CardItem model) : ObservableObject
{
    public CardItem Model { get; } = model;

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

    public int ColumnId
    {
        get => Model.ColumnId;
        set
        {
            if (Model.ColumnId == value) return;
            Model.ColumnId = value;
            OnPropertyChanged();
        }
    }

    public int? ProjectId
    {
        get => Model.ProjectId;
        set
        {
            if (Model.ProjectId == value) return;
            Model.ProjectId = value;
            OnPropertyChanged();
        }
    }

    private string _projectName = string.Empty;
    public string ProjectName
    {
        get => _projectName;
        set => SetField(ref _projectName, value);
    }
}
