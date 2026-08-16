namespace KanbanApp.ViewModels;

public class ImportedRowEditViewModel(CardViewModel card) : ObservableObject
{
    public CardViewModel Card { get; } = card;
    public int Id => Card.Id;

    private string _title = card.Title;
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    private int _columnId = card.ColumnId;
    public int ColumnId
    {
        get => _columnId;
        set => SetField(ref _columnId, value);
    }

    private string _priority = card.Priority;
    public string Priority
    {
        get => _priority;
        set => SetField(ref _priority, value);
    }

    private int? _projectId = card.ProjectId;
    public int? ProjectId
    {
        get => _projectId;
        set => SetField(ref _projectId, value);
    }

    private int? _goalId = card.GoalId;
    public int? GoalId
    {
        get => _goalId;
        set => SetField(ref _goalId, value);
    }

    private DateTime? _dueDate = card.DueDate;
    public DateTime? DueDate
    {
        get => _dueDate;
        set => SetField(ref _dueDate, value);
    }

    private string? _who = card.Who;
    public string? Who
    {
        get => _who;
        set => SetField(ref _who, value);
    }

    private bool _isImported = card.IsImported;
    public bool IsImported
    {
        get => _isImported;
        set => SetField(ref _isImported, value);
    }
}
