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

    public DateTime? ArchivedAt
    {
        get => Model.ArchivedAt;
        set
        {
            if (Model.ArchivedAt == value) return;
            Model.ArchivedAt = value;
            OnPropertyChanged();
        }
    }

    public int? WhoId
    {
        get => Model.WhoId;
        set
        {
            if (Model.WhoId == value) return;
            Model.WhoId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEmailCard));
        }
    }

    private string _whoName = string.Empty;
    public string WhoName
    {
        get => _whoName;
        set
        {
            if (SetField(ref _whoName, value))
            {
                OnPropertyChanged(nameof(WhoDisplay));
            }
        }
    }

    private string? _whoEmail;
    public string? WhoEmail
    {
        get => _whoEmail;
        set
        {
            if (SetField(ref _whoEmail, value))
            {
                OnPropertyChanged(nameof(CanEmailCard));
            }
        }
    }

    public string WhoDisplay => string.IsNullOrWhiteSpace(WhoName) || WhoName == "Unassigned" ? string.Empty : $"Assigned: {WhoName}";

    public bool CanEmailCard => WhoId.HasValue && !string.IsNullOrWhiteSpace(WhoEmail);

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

    public string? WebsiteUrl
    {
        get => Model.WebsiteUrl;
        set
        {
            if (Model.WebsiteUrl == value) return;
            Model.WebsiteUrl = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWebsiteUrl));
        }
    }

    public bool HasWebsiteUrl => !string.IsNullOrWhiteSpace(WebsiteUrl);

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

    public bool ForceEditOnComplete
    {
        get => Model.ForceEditOnComplete;
        set
        {
            if (Model.ForceEditOnComplete == value) return;
            Model.ForceEditOnComplete = value;
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

    // True once this specific card has already spawned its next occurrence on completion - prevents
    // a duplicate spawn if the card is later reactivated (e.g. from Archive) and marked Done again.
    public bool NextOccurrenceSpawned
    {
        get => Model.NextOccurrenceSpawned;
        set
        {
            if (Model.NextOccurrenceSpawned == value) return;
            Model.NextOccurrenceSpawned = value;
            OnPropertyChanged();
        }
    }

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

    private bool _isOverdue;
    public bool IsOverdue
    {
        get => _isOverdue;
        set => SetField(ref _isOverdue, value);
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

    private List<AttachmentViewModel> _attachments = [];
    public List<AttachmentViewModel> Attachments
    {
        get => _attachments;
        set
        {
            _attachments = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAttachments));
            OnPropertyChanged(nameof(AttachmentsCountDisplay));
        }
    }

    public bool HasAttachments => Attachments.Count > 0;

    public string AttachmentsCountDisplay =>
        Attachments.Count == 0 ? string.Empty : $"📎 {Attachments.Count} attachment{(Attachments.Count == 1 ? "" : "s")}";
}
