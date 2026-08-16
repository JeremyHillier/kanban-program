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

    public int? GoalId
    {
        get => Model.GoalId;
        set
        {
            if (Model.GoalId == value) return;
            Model.GoalId = value;
            OnPropertyChanged();
        }
    }

    private string _goalName = string.Empty;
    public string GoalName
    {
        get => _goalName;
        set
        {
            if (SetField(ref _goalName, value))
            {
                OnPropertyChanged(nameof(GoalDisplay));
            }
        }
    }

    public string GoalDisplay => string.IsNullOrWhiteSpace(GoalName) || GoalName == "No Goal" ? string.Empty : $"Goal: {GoalName}";

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

    public string? Notes
    {
        get => Model.Notes;
        set
        {
            if (Model.Notes == value) return;
            Model.Notes = value;
            OnPropertyChanged();
        }
    }

    public bool IsImported
    {
        get => Model.IsImported;
        set
        {
            if (Model.IsImported == value) return;
            Model.IsImported = value;
            OnPropertyChanged();
        }
    }

    public bool IsRecurring
    {
        get => Model.IsRecurring;
        set
        {
            if (Model.IsRecurring == value) return;
            Model.IsRecurring = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecurrenceDisplay));
        }
    }

    public string? RecurrencePattern
    {
        get => Model.RecurrencePattern;
        set
        {
            if (Model.RecurrencePattern == value) return;
            Model.RecurrencePattern = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecurrenceDisplay));
        }
    }

    public string RecurrenceDisplay => IsRecurring && !string.IsNullOrWhiteSpace(RecurrencePattern) ? $"↻ Repeats {RecurrencePattern}" : string.Empty;

    public DateTime? LastUpdated
    {
        get => Model.LastUpdated;
        set
        {
            if (Model.LastUpdated == value) return;
            Model.LastUpdated = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastUpdatedDisplay));
        }
    }

    public string LastUpdatedDisplay => LastUpdated is null ? "Updated: unknown" : $"Updated {LastUpdated:MMM d, h:mm tt}";

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    private List<FlagViewModel> _flags = [];
    public List<FlagViewModel> Flags
    {
        get => _flags;
        set
        {
            _flags = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FlagsDisplay));
        }
    }

    public string FlagsDisplay => Flags.Count == 0 ? string.Empty : $"Flags: {string.Join(", ", Flags.Select(f => f.Name))}";

    private List<SubTaskViewModel> _subTasks = [];
    public List<SubTaskViewModel> SubTasks
    {
        get => _subTasks;
        set
        {
            _subTasks = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSubTasks));
            OnPropertyChanged(nameof(SubTaskProgressDisplay));
        }
    }

    public bool HasSubTasks => SubTasks.Count > 0;

    public string SubTaskProgressDisplay
    {
        get
        {
            if (SubTasks.Count == 0) return string.Empty;
            var done = SubTasks.Count(s => s.IsDone);
            return $"Subtasks: {done}/{SubTasks.Count} ({done * 100 / SubTasks.Count}%)";
        }
    }

    public void RefreshSubTaskProgress()
    {
        OnPropertyChanged(nameof(SubTaskProgressDisplay));
    }
}
