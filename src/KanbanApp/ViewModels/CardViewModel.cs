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
            OnPropertyChanged(nameof(DueDateTime));
        }
    }

    public string? DueTime
    {
        get => Model.DueTime;
        set
        {
            if (Model.DueTime == value) return;
            Model.DueTime = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DueDateDisplay));
            OnPropertyChanged(nameof(DueDateTime));
        }
    }

    // Optional "not before" date. A task whose start date is still ahead counts as not started:
    // the board can hide those (MainViewModel.HideFutureTasks) and the card says when it starts.
    public DateTime? StartDate
    {
        get => Model.StartDate;
        set
        {
            if (Model.StartDate == value) return;
            Model.StartDate = value;
            OnPropertyChanged();
            RefreshStartDisplay();
        }
    }

    public bool IsNotStarted => StartDate is not null && StartDate.Value.Date > DateTime.Today;

    // Only while the start is still ahead - once it has passed the line is just clutter.
    public string StartDateDisplay => IsNotStarted ? $"Starts {StartDate:MMM d, yyyy}" : string.Empty;

    // Also called when the day changes, since both depend on today's date.
    public void RefreshStartDisplay()
    {
        OnPropertyChanged(nameof(IsNotStarted));
        OnPropertyChanged(nameof(StartDateDisplay));
    }

    // Who or what the task is blocked by, free text. Anything non-blank means the task is waiting:
    // the card says so, and the Waiting On button shows just those tasks.
    public string? WaitingOn
    {
        get => Model.WaitingOn;
        set
        {
            var cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (Model.WaitingOn == cleaned) return;
            Model.WaitingOn = cleaned;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWaiting));
            OnPropertyChanged(nameof(WaitingOnDisplay));
        }
    }

    public bool IsWaiting => !string.IsNullOrWhiteSpace(WaitingOn);

    public string WaitingOnDisplay => IsWaiting ? $"Waiting on: {WaitingOn}" : string.Empty;

    // The exact moment a timed task comes due - null unless both a date and a time are set.
    public DateTime? DueDateTime =>
        DueDate is not null && TimeSpan.TryParse(DueTime, out var time) ? DueDate.Value.Date + time : null;

    public string DueDateDisplay => DueDate is null ? string.Empty
        : DueDateTime is { } at ? $"Due {at:MMM d, yyyy h:mm tt}" : $"Due {DueDate:MMM d, yyyy}";

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

    // Everyone the task is assigned to, in the order they were picked. The first is the lead:
    // WhoId, LeadName and WhoEmail are the lead's, and Sort by Who goes by the lead. WhoName is
    // everyone, for display ("Alice, Bob"). Setting this is the only way any of them change.
    private List<PersonViewModel> _people = [];
    public IReadOnlyList<PersonViewModel> People
    {
        get => _people;
        set
        {
            _people = value.DistinctBy(p => p.Id).ToList();
            Model.WhoId = _people.Count == 0 ? null : _people[0].Id;
            Model.PeopleIds = _people.Select(p => p.Id).ToList();
            RefreshPeople();
        }
    }

    // Also called when a person is renamed or their email changes - the card holds the same
    // PersonViewModel objects as the People list, so only the notifications are needed.
    public void RefreshPeople()
    {
        OnPropertyChanged(nameof(People));
        OnPropertyChanged(nameof(WhoId));
        OnPropertyChanged(nameof(LeadName));
        OnPropertyChanged(nameof(WhoName));
        OnPropertyChanged(nameof(WhoEmail));
        OnPropertyChanged(nameof(WhoDisplay));
        OnPropertyChanged(nameof(PeopleEmails));
        OnPropertyChanged(nameof(CanEmailCard));
    }

    public int? WhoId => Model.WhoId;

    public string LeadName => _people.Count == 0 ? "Unassigned" : _people[0].Name;

    public string WhoName => _people.Count == 0 ? "Unassigned" : string.Join(", ", _people.Select(p => p.Name));

    public string? WhoEmail => _people.Count == 0 ? null : _people[0].Email;

    public bool IsAssignedTo(int personId) => _people.Any(p => p.Id == personId);

    // Everyone assigned who has an email address, lead first - who Email This Task goes to.
    public IReadOnlyList<string> PeopleEmails => _people
        .Select(p => p.Email?.Trim()).Where(e => !string.IsNullOrEmpty(e)).Select(e => e!)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public string WhoDisplay => _people.Count == 0 ? string.Empty : $"Assigned: {WhoName}";

    public bool CanEmailCard => PeopleEmails.Count > 0;

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

    // How many times the task still happens, counting this one. Null keeps it repeating until it is
    // deleted or Recurring is unticked; 1 means this is the last one and completing it creates no
    // more. Each new occurrence carries one fewer.
    public int? RecurrencesLeft
    {
        get => Model.RecurrencesLeft;
        set
        {
            if (Model.RecurrencesLeft == value) return;
            Model.RecurrencesLeft = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecurrenceDisplay));
        }
    }

    // Whether finishing (or skipping) this task should put the next one on the board.
    public bool HasNextOccurrence =>
        IsRecurring && !string.IsNullOrWhiteSpace(RecurrencePattern) && !NextOccurrenceSpawned && RecurrencesLeft is null or > 1;

    public string RecurrenceDisplay => !IsRecurring || string.IsNullOrWhiteSpace(RecurrencePattern) ? string.Empty
        : RecurrencesLeft switch
        {
            null => $"↻ Repeats {RecurrencePattern}",
            <= 1 => $"↻ Repeats {RecurrencePattern} · last one",
            var left => $"↻ Repeats {RecurrencePattern} · {left - 1} more"
        };

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
            OnPropertyChanged(nameof(StatusStampDisplay));
        }
    }

    public string LastUpdatedDisplay => LastUpdated is null ? "Updated: unknown" : $"Updated {LastUpdated:MMM d, h:mm tt}";

    public DateTime? CompletedAt
    {
        get => Model.CompletedAt;
        set
        {
            if (Model.CompletedAt == value) return;
            Model.CompletedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusStampDisplay));
        }
    }

    // The small stamp on the card: when it was finished once it's Done (editing it afterwards
    // doesn't move this), otherwise when it was last touched. The year is added only when it isn't
    // this year, since a finished task can sit in Done for a long time before being archived.
    public string StatusStampDisplay => CompletedAt is { } completed
        ? $"Completed {completed.ToString(completed.Year == DateTime.Today.Year ? "MMM d, h:mm tt" : "MMM d, yyyy, h:mm tt")}"
        : LastUpdatedDisplay;

    // The fuller stamps shown beside the Help button in the Edit Task dialog, where there's room for
    // the year and both times can be seen at once.
    public string? CompletedFullDisplay => CompletedAt is { } completed ? $"Completed {completed:MMM d, yyyy, h:mm tt}" : null;
    public string UpdatedFullDisplay => LastUpdated is { } updated ? $"Last updated {updated:MMM d, yyyy, h:mm tt}" : "Last updated: unknown";

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    // Picked for a multi-card drag (Ctrl/Shift+click). Screen state only, never saved.
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
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

    // Used by Undo: copies every stored value from a freshly loaded copy of this same task onto
    // the card already on the board, so anything still holding this card (an open reminder, a drag,
    // the selection) keeps pointing at the live one. Screen state (IsVisible, IsSelected) is left
    // alone. A new stored field needs adding here as well as to the loader.
    public void TakeValuesFrom(CardViewModel other)
    {
        Title = other.Title;
        ColumnId = other.ColumnId;
        ProjectId = other.ProjectId;
        ProjectName = other.ProjectName;
        GoalId = other.GoalId;
        GoalName = other.GoalName;
        Priority = other.Priority;
        DueDate = other.DueDate;
        DueTime = other.DueTime;
        StartDate = other.StartDate;
        WaitingOn = other.WaitingOn;
        People = other.People;
        Notes = other.Notes;
        WebsiteUrl = other.WebsiteUrl;
        IsImported = other.IsImported;
        ForceEditOnComplete = other.ForceEditOnComplete;
        IsRecurring = other.IsRecurring;
        RecurrencePattern = other.RecurrencePattern;
        NextOccurrenceSpawned = other.NextOccurrenceSpawned;
        RecurrencesLeft = other.RecurrencesLeft;
        LastUpdated = other.LastUpdated;
        CompletedAt = other.CompletedAt;
        Flags = other.Flags;
        SubTasks = other.SubTasks;
        Attachments = other.Attachments;
    }
}
