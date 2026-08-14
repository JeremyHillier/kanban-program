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

    public string Priority
    {
        get => Model.Priority;
        set
        {
            if (Model.Priority == value) return;
            Model.Priority = value;
            OnPropertyChanged();
        }
    }

    public DateTime? DueDate
    {
        get => Model.DueDate;
        set
        {
            if (Model.DueDate == value) return;
            Model.DueDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DueDateDisplay));
        }
    }

    public string DueDateDisplay => DueDate is null ? string.Empty : $"Due {DueDate:MMM d, yyyy}";

    public string? Who
    {
        get => Model.Who;
        set
        {
            if (Model.Who == value) return;
            Model.Who = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WhoDisplay));
        }
    }

    public string WhoDisplay => string.IsNullOrWhiteSpace(Who) ? string.Empty : $"Assigned: {Who}";
}
