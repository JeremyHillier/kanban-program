using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public string AppVersion { get; } = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public string CopyrightText { get; } = "© Jeremy Hillier Consulting Inc";

    private enum SortMode { ProjectThenDueDate, DueDateThenProject }

    private SortMode _currentSortMode = SortMode.ProjectThenDueDate;

    private bool _isDarkMode;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set
        {
            if (SetField(ref _isDarkMode, value))
            {
                OnPropertyChanged(nameof(ThemeButtonLabel));
            }
        }
    }

    public string ThemeButtonLabel => IsDarkMode ? "Light Mode" : "Dark Mode";

    private static readonly Brush[] ColumnPalette =
    [
        new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xEF)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xCD)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0xE1, 0xD5, 0xF5)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA)), // Done - green
    ];

    public ObservableCollection<ColumnViewModel> Columns { get; } = [];
    public ObservableCollection<ProjectViewModel> Projects { get; } = [];
    public ObservableCollection<GoalViewModel> Goals { get; } = [];

    public ObservableCollection<string> ProjectFilterOptions { get; } = ["All"];
    public ObservableCollection<string> PriorityFilterOptions { get; } = ["All", "High", "Medium", "Normal"];
    public ObservableCollection<string> WhoFilterOptions { get; } = ["All"];
    public ObservableCollection<string> GoalFilterOptions { get; } = ["All"];

    private string _selectedProjectFilter = "All";
    public string SelectedProjectFilter
    {
        get => _selectedProjectFilter;
        set { if (SetField(ref _selectedProjectFilter, value ?? "All")) ApplyFilters(); }
    }

    private string _selectedPriorityFilter = "All";
    public string SelectedPriorityFilter
    {
        get => _selectedPriorityFilter;
        set { if (SetField(ref _selectedPriorityFilter, value ?? "All")) ApplyFilters(); }
    }

    private string _selectedWhoFilter = "All";
    public string SelectedWhoFilter
    {
        get => _selectedWhoFilter;
        set { if (SetField(ref _selectedWhoFilter, value ?? "All")) ApplyFilters(); }
    }

    private string _selectedGoalFilter = "All";
    public string SelectedGoalFilter
    {
        get => _selectedGoalFilter;
        set { if (SetField(ref _selectedGoalFilter, value ?? "All")) ApplyFilters(); }
    }

    private string _dueFilter = string.Empty;
    public string DueFilter
    {
        get => _dueFilter;
        set { if (SetField(ref _dueFilter, value)) ApplyFilters(); }
    }

    private string _keywordFilter = string.Empty;
    public string KeywordFilter
    {
        get => _keywordFilter;
        set { if (SetField(ref _keywordFilter, value)) ApplyFilters(); }
    }

    public RelayCommand DeleteCardCommand { get; }
    public RelayCommand MoveCardCommand { get; }

    public MainViewModel(DatabaseService db)
    {
        _db = db;

        DeleteCardCommand = new RelayCommand(param => DeleteCard(param as CardViewModel));
        MoveCardCommand = new RelayCommand(param =>
        {
            if (param is (CardViewModel card, ColumnViewModel targetColumn))
            {
                MoveCard(card, targetColumn);
            }
        });

        Load();

        _isDarkMode = _db.GetSetting("Theme") == "Dark";
        Theming.ThemeManager.Apply(_isDarkMode);
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        _db.SetSetting("Theme", IsDarkMode ? "Dark" : "Light");
        Theming.ThemeManager.Apply(IsDarkMode);
    }

    private void Load()
    {
        Columns.Clear();
        Projects.Clear();
        Goals.Clear();

        foreach (var project in _db.GetProjects())
        {
            Projects.Add(new ProjectViewModel(project));
        }

        foreach (var goal in _db.GetGoals())
        {
            Goals.Add(new GoalViewModel(goal));
        }

        var columns = _db.GetColumns();
        var cards = _db.GetCards();

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var background = ColumnPalette[i % ColumnPalette.Length];
            var columnVm = new ColumnViewModel(column, background);
            foreach (var card in cards.Where(c => c.ColumnId == column.Id))
            {
                var cardVm = new CardViewModel(card)
                {
                    ProjectName = ResolveProjectName(card.ProjectId),
                    GoalName = ResolveGoalName(card.GoalId),
                    LastUpdated = card.LastUpdated
                };
                columnVm.Cards.Add(cardVm);
            }
            Columns.Add(columnVm);
        }

        RefreshProjectFilterOptions();
        RefreshWhoFilterOptions();
        RefreshGoalFilterOptions();
        ApplySort();
    }

    public void SortByProject()
    {
        _currentSortMode = SortMode.ProjectThenDueDate;
        ApplySort();
    }

    public void SortByDueDate()
    {
        _currentSortMode = SortMode.DueDateThenProject;
        ApplySort();
    }

    private void ApplySort()
    {
        var updates = new List<(int, int)>();

        foreach (var column in Columns)
        {
            var sorted = _currentSortMode == SortMode.DueDateThenProject
                ? column.Cards.OrderBy(c => c.DueDate ?? DateTime.MaxValue).ThenBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase).ToList()
                : column.Cards.OrderBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.DueDate ?? DateTime.MaxValue).ToList();

            column.Cards.Clear();
            for (var i = 0; i < sorted.Count; i++)
            {
                column.Cards.Add(sorted[i]);
                updates.Add((sorted[i].Id, i));
            }
        }

        _db.UpdateSortOrders(updates);
    }

    private void RefreshProjectFilterOptions()
    {
        var current = SelectedProjectFilter;
        ProjectFilterOptions.Clear();
        ProjectFilterOptions.Add("All");
        foreach (var project in Projects.OrderBy(p => p.Name))
        {
            ProjectFilterOptions.Add(project.Name);
        }

        if (!ProjectFilterOptions.Contains(current))
        {
            SelectedProjectFilter = "All";
        }
        OnPropertyChanged(nameof(SelectedProjectFilter));
    }

    private void RefreshWhoFilterOptions()
    {
        var current = SelectedWhoFilter;
        WhoFilterOptions.Clear();
        WhoFilterOptions.Add("All");
        foreach (var who in Columns.SelectMany(c => c.Cards)
                     .Select(c => c.Who)
                     .Where(w => !string.IsNullOrWhiteSpace(w))
                     .Distinct()
                     .OrderBy(w => w))
        {
            WhoFilterOptions.Add(who!);
        }

        if (!WhoFilterOptions.Contains(current))
        {
            SelectedWhoFilter = "All";
        }
        OnPropertyChanged(nameof(SelectedWhoFilter));
    }

    private void RefreshGoalFilterOptions()
    {
        var current = SelectedGoalFilter;
        GoalFilterOptions.Clear();
        GoalFilterOptions.Add("All");
        foreach (var goal in Goals.OrderBy(g => g.Name))
        {
            GoalFilterOptions.Add(goal.Name);
        }

        if (!GoalFilterOptions.Contains(current))
        {
            SelectedGoalFilter = "All";
        }
        OnPropertyChanged(nameof(SelectedGoalFilter));
    }

    public void ApplyFilters()
    {
        foreach (var card in Columns.SelectMany(c => c.Cards))
        {
            card.IsVisible = MatchesFilters(card);
        }
    }

    private bool MatchesFilters(CardViewModel card)
    {
        if (SelectedProjectFilter != "All" && card.ProjectName != SelectedProjectFilter) return false;
        if (SelectedPriorityFilter != "All" && card.Priority != SelectedPriorityFilter) return false;
        if (SelectedWhoFilter != "All" && (card.Who ?? string.Empty) != SelectedWhoFilter) return false;
        if (SelectedGoalFilter != "All" && card.GoalName != SelectedGoalFilter) return false;

        if (!string.IsNullOrEmpty(DueFilter))
        {
            var today = DateTime.Today;
            var matchesDue = DueFilter switch
            {
                "Today" => card.DueDate?.Date == today,
                "Tomorrow" => card.DueDate?.Date == today.AddDays(1),
                "Within a Week" => card.DueDate is not null && card.DueDate.Value.Date >= today && card.DueDate.Value.Date <= today.AddDays(7),
                "No Due Date" => card.DueDate is null,
                _ => true
            };
            if (!matchesDue) return false;
        }

        if (!string.IsNullOrWhiteSpace(KeywordFilter))
        {
            var keyword = KeywordFilter.Trim();
            var matchesKeyword = card.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || card.ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (card.Who ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase);
            if (!matchesKeyword) return false;
        }

        return true;
    }

    public void ClearFilters()
    {
        _selectedProjectFilter = "All";
        OnPropertyChanged(nameof(SelectedProjectFilter));
        _selectedPriorityFilter = "All";
        OnPropertyChanged(nameof(SelectedPriorityFilter));
        _selectedWhoFilter = "All";
        OnPropertyChanged(nameof(SelectedWhoFilter));
        _selectedGoalFilter = "All";
        OnPropertyChanged(nameof(SelectedGoalFilter));
        _dueFilter = string.Empty;
        OnPropertyChanged(nameof(DueFilter));
        _keywordFilter = string.Empty;
        OnPropertyChanged(nameof(KeywordFilter));

        ApplyFilters();
    }

    private string ResolveProjectName(int? projectId)
    {
        if (projectId is null) return "No Project";
        return Projects.FirstOrDefault(p => p.Id == projectId)?.Name ?? "No Project";
    }

    private string ResolveGoalName(int? goalId)
    {
        if (goalId is null) return "No Goal";
        return Goals.FirstOrDefault(g => g.Id == goalId)?.Name ?? "No Goal";
    }

    public void AddCard(string title, ColumnViewModel column, ProjectViewModel? project, string priority, DateTime? dueDate, string? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        var card = _db.AddCard(column.Id, title.Trim(), project?.Id, column.Name, priority, dueDate, who, isRecurring, recurrencePattern, goal?.Id);
        var cardVm = new CardViewModel(card)
        {
            ProjectName = project?.Name ?? "No Project",
            GoalName = goal?.Name ?? "No Goal",
            LastUpdated = card.LastUpdated
        };
        column.Cards.Add(cardVm);

        RefreshWhoFilterOptions();
        cardVm.IsVisible = MatchesFilters(cardVm);
        ApplySort();
    }

    public void EditCard(CardViewModel card, string title, ColumnViewModel newColumn, ProjectViewModel? project, string priority, DateTime? dueDate, string? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        card.Title = title.Trim();
        card.ProjectId = project?.Id;
        card.ProjectName = project?.Name ?? "No Project";
        card.Priority = priority;
        card.DueDate = dueDate;
        card.Who = who;
        card.IsRecurring = isRecurring;
        card.RecurrencePattern = recurrencePattern;
        card.GoalId = goal?.Id;
        card.GoalName = goal?.Name ?? "No Goal";

        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, project?.Id, priority, dueDate, who, isRecurring, recurrencePattern, goal?.Id);

        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is not null && sourceColumn != newColumn)
        {
            MoveCard(card, newColumn);
        }

        RefreshWhoFilterOptions();
        card.IsVisible = MatchesFilters(card);
        ApplySort();
    }

    public void AddProject(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var project = _db.AddProject(name.Trim());
        Projects.Add(new ProjectViewModel(project));
        RefreshProjectFilterOptions();
    }

    public void RenameProject(ProjectViewModel project, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || project.Name == newName.Trim()) return;

        project.Name = newName.Trim();
        _db.RenameProject(project.Id, project.Name);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.ProjectId == project.Id))
        {
            card.ProjectName = project.Name;
        }

        RefreshProjectFilterOptions();
    }

    public void DeleteProject(ProjectViewModel project)
    {
        _db.DeleteProject(project.Id);
        Projects.Remove(project);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.ProjectId == project.Id))
        {
            card.ProjectId = null;
            card.ProjectName = "No Project";
        }

        RefreshProjectFilterOptions();
    }

    public void AddGoal(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var goal = _db.AddGoal(name.Trim());
        Goals.Add(new GoalViewModel(goal));
        RefreshGoalFilterOptions();
    }

    public void RenameGoal(GoalViewModel goal, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || goal.Name == newName.Trim()) return;

        goal.Name = newName.Trim();
        _db.RenameGoal(goal.Id, goal.Name);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.GoalId == goal.Id))
        {
            card.GoalName = goal.Name;
        }

        RefreshGoalFilterOptions();
    }

    public void DeleteGoal(GoalViewModel goal)
    {
        _db.DeleteGoal(goal.Id);
        Goals.Remove(goal);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.GoalId == goal.Id))
        {
            card.GoalId = null;
            card.GoalName = "No Goal";
        }

        RefreshGoalFilterOptions();
    }

    private void DeleteCard(CardViewModel? card)
    {
        if (card is null) return;

        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        column?.Cards.Remove(card);
        _db.DeleteCard(card.Id, card.Title, column?.Name ?? "Unknown");
    }

    private void MoveCard(CardViewModel card, ColumnViewModel targetColumn)
    {
        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is null || sourceColumn == targetColumn) return;

        sourceColumn.Cards.Remove(card);
        card.ColumnId = targetColumn.Id;
        targetColumn.Cards.Add(card);

        card.LastUpdated = _db.MoveCard(card.Id, targetColumn.Id, card.Title, sourceColumn.Name, targetColumn.Name);

        if (targetColumn.Name == "Done" && card.IsRecurring && !string.IsNullOrWhiteSpace(card.RecurrencePattern))
        {
            SpawnNextOccurrence(card);
        }

        ApplySort();
    }

    private void SpawnNextOccurrence(CardViewModel completedCard)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do");
        if (toDoColumn is null) return;

        var nextDueDate = CalculateNextDueDate(completedCard.DueDate ?? DateTime.Today, completedCard.RecurrencePattern!);
        var project = Projects.FirstOrDefault(p => p.Id == completedCard.ProjectId);
        var goal = Goals.FirstOrDefault(g => g.Id == completedCard.GoalId);

        AddCard(completedCard.Title, toDoColumn, project, completedCard.Priority, nextDueDate, completedCard.Who,
            true, completedCard.RecurrencePattern, goal);
    }

    private static DateTime CalculateNextDueDate(DateTime anchor, string pattern) => pattern switch
    {
        "Daily" => anchor.AddDays(1),
        "Weekday" => NextWeekday(anchor),
        "Weekly" => anchor.AddDays(7),
        "Monthly" => anchor.AddMonths(1),
        "Annually" => anchor.AddYears(1),
        _ => anchor.AddDays(1)
    };

    private static DateTime NextWeekday(DateTime date)
    {
        var next = date.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }
        return next;
    }

    public void ArchiveDoneTasks()
    {
        var doneColumn = Columns.FirstOrDefault(c => c.Name == "Done");
        if (doneColumn is null) return;

        foreach (var card in doneColumn.Cards.ToList())
        {
            _db.ArchiveCard(card.Id, card.Title, doneColumn.Name);
            doneColumn.Cards.Remove(card);
        }
    }

    public List<ArchivedCardInfo> GetArchivedCards() => _db.GetArchivedCards();
}
