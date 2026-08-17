using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public string AppVersion { get; } = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public string CopyrightText { get; } = "© Jeremy Hillier Consulting Inc";
    public bool IsTestChannel => AppChannel.IsTest;
    public string WindowTitle => AppChannel.DisplayName;

    private enum SortMode { ProjectThenDueDate, DueDateThenProject, WhoThenDueDate, PriorityThenDueDate }

    private SortMode _currentSortMode = SortMode.ProjectThenDueDate;

    private string _selectedSortMode = "Project";
    public string SelectedSortMode
    {
        get => _selectedSortMode;
        private set => SetField(ref _selectedSortMode, value);
    }

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

    private bool _isButtonsOnRight;
    public bool IsButtonsOnRight
    {
        get => _isButtonsOnRight;
        set
        {
            if (SetField(ref _isButtonsOnRight, value))
            {
                OnPropertyChanged(nameof(SidebarDock));
            }
        }
    }

    public Dock SidebarDock => IsButtonsOnRight ? Dock.Right : Dock.Left;

    private bool _isCompactCards;
    public bool IsCompactCards
    {
        get => _isCompactCards;
        set
        {
            if (SetField(ref _isCompactCards, value))
            {
                OnPropertyChanged(nameof(IsLargeCards));
                OnPropertyChanged(nameof(CardSizeButtonLabel));
            }
        }
    }

    public bool IsLargeCards => !IsCompactCards;
    public string CardSizeButtonLabel => IsCompactCards ? "Large Cards" : "Compact Cards";

    private int _columnWidth = 310;
    public int ColumnWidth
    {
        get => _columnWidth;
        set => SetField(ref _columnWidth, value);
    }

    public void SetColumnWidth(int value)
    {
        ColumnWidth = Math.Clamp(value, 150, 800);
        _db.SetSetting("ColumnWidth", ColumnWidth.ToString());
    }

    public string CurrentDbPath => _db.DbPath;
    public string AttachmentsDir => DatabaseService.GetAttachmentsDir(_db.DbPath);

    public bool ShowSplash { get; private set; }
    public int SplashDelayMs { get; private set; }

    public void SetShowSplash(bool value)
    {
        ShowSplash = value;
        _db.SetSetting("ShowSplash", value ? "True" : "False");
    }

    public void SetSplashDelayMs(int value)
    {
        SplashDelayMs = value;
        _db.SetSetting("SplashDelayMs", value.ToString());
    }

    public string DefaultExportPath { get; private set; } = string.Empty;
    public string DefaultImportPath { get; private set; } = string.Empty;
    public string LinkedFilesDefaultPath { get; private set; } = string.Empty;

    public void SetDefaultExportPath(string value)
    {
        DefaultExportPath = value;
        _db.SetSetting("DefaultExportPath", value);
    }

    public void SetDefaultImportPath(string value)
    {
        DefaultImportPath = value;
        _db.SetSetting("DefaultImportPath", value);
    }

    public void SetLinkedFilesDefaultPath(string value)
    {
        LinkedFilesDefaultPath = value;
        _db.SetSetting("LinkedFilesDefaultPath", value);
    }

    public bool StartFullScreen { get; private set; }
    public bool ConfirmDelete { get; private set; } = true;
    public bool ConfirmArchive { get; private set; } = true;
    public bool AddNoteOnComplete { get; private set; }

    public void SetStartFullScreen(bool value)
    {
        StartFullScreen = value;
        _db.SetSetting("StartFullScreen", value ? "True" : "False");
    }

    public void SetConfirmDelete(bool value)
    {
        ConfirmDelete = value;
        _db.SetSetting("ConfirmDelete", value ? "True" : "False");
    }

    public void SetConfirmArchive(bool value)
    {
        ConfirmArchive = value;
        _db.SetSetting("ConfirmArchive", value ? "True" : "False");
    }

    public void SetAddNoteOnComplete(bool value)
    {
        AddNoteOnComplete = value;
        _db.SetSetting("AddNoteOnComplete", value ? "True" : "False");
    }

    private static readonly Brush[] ColumnPalette =
    [
        new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xEF)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xCD)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0xE1, 0xD5, 0xF5)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA)), // Done - green
    ];

    public int OpenTaskCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards).Count();

    public int OverdueCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate is not null && c.DueDate.Value.Date < DateTime.Today);

    public int DueTodayCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate?.Date == DateTime.Today);

    public int DueThisWeekCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate is not null && c.DueDate.Value.Date >= DateTime.Today && c.DueDate.Value.Date <= DateTime.Today.AddDays(7));

    private void RefreshDashboardStats()
    {
        OnPropertyChanged(nameof(OpenTaskCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(DueTodayCount));
        OnPropertyChanged(nameof(DueThisWeekCount));

        foreach (var column in Columns)
        {
            var canBeOverdue = column.Name != "Done";
            foreach (var card in column.Cards)
            {
                card.IsOverdue = canBeOverdue && card.DueDate is not null && card.DueDate.Value.Date < DateTime.Today;
            }
        }
    }

    public ObservableCollection<ColumnViewModel> Columns { get; } = [];
    public ObservableCollection<ProjectViewModel> Projects { get; } = [];
    public ObservableCollection<GoalViewModel> Goals { get; } = [];
    public ObservableCollection<FlagViewModel> Flags { get; } = [];
    public ObservableCollection<PersonViewModel> People { get; } = [];

    public IEnumerable<ProjectViewModel> ActiveProjects => Projects.Where(p => p.IsActive);
    public IEnumerable<GoalViewModel> ActiveGoals => Goals.Where(g => g.IsActive);
    public IEnumerable<FlagViewModel> ActiveFlags => Flags.Where(f => f.IsActive);
    public IEnumerable<PersonViewModel> ActivePeople => People.Where(p => p.IsActive);

    public ObservableCollection<string> ProjectFilterOptions { get; } = ["All"];
    public ObservableCollection<string> PriorityFilterOptions { get; } = ["All", "High", "Medium", "Normal"];
    public ObservableCollection<string> WhoFilterOptions { get; } = ["All"];
    public ObservableCollection<string> GoalFilterOptions { get; } = ["All"];
    public ObservableCollection<string> FlagFilterOptions { get; } = ["All"];
    public ObservableCollection<string> DueFilterOptions { get; } = ["All", "Today", "Tomorrow", "Within a Week", "No Due Date"];

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

    private string _selectedFlagFilter = "All";
    public string SelectedFlagFilter
    {
        get => _selectedFlagFilter;
        set { if (SetField(ref _selectedFlagFilter, value ?? "All")) ApplyFilters(); }
    }

    private string _dueFilter = "All";
    public string DueFilter
    {
        get => _dueFilter;
        set { if (SetField(ref _dueFilter, value ?? "All")) ApplyFilters(); }
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

        _isButtonsOnRight = _db.GetSetting("ButtonPosition") == "Right";

        _isCompactCards = _db.GetSetting("CardSize") == "Compact";
        _columnWidth = int.TryParse(_db.GetSetting("ColumnWidth"), out var columnWidth) ? columnWidth : 310;

        ShowSplash = _db.GetSetting("ShowSplash") != "False";
        SplashDelayMs = int.TryParse(_db.GetSetting("SplashDelayMs"), out var delay) ? delay : 1800;

        DefaultExportPath = _db.GetSetting("DefaultExportPath") ?? string.Empty;
        DefaultImportPath = _db.GetSetting("DefaultImportPath") ?? string.Empty;
        LinkedFilesDefaultPath = _db.GetSetting("LinkedFilesDefaultPath") ?? string.Empty;

        StartFullScreen = _db.GetSetting("StartFullScreen") == "True";
        ConfirmDelete = _db.GetSetting("ConfirmDelete") != "False";
        ConfirmArchive = _db.GetSetting("ConfirmArchive") != "False";
        AddNoteOnComplete = _db.GetSetting("AddNoteOnComplete") == "True";
    }

    public void ToggleButtonPosition()
    {
        IsButtonsOnRight = !IsButtonsOnRight;
        _db.SetSetting("ButtonPosition", IsButtonsOnRight ? "Right" : "Left");
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        _db.SetSetting("Theme", IsDarkMode ? "Dark" : "Light");
        Theming.ThemeManager.Apply(IsDarkMode);
    }

    public void ToggleCardSize()
    {
        IsCompactCards = !IsCompactCards;
        _db.SetSetting("CardSize", IsCompactCards ? "Compact" : "Large");
    }

    private void Load()
    {
        Columns.Clear();
        Projects.Clear();
        Goals.Clear();
        Flags.Clear();
        People.Clear();

        foreach (var project in _db.GetProjects())
        {
            Projects.Add(new ProjectViewModel(project));
        }

        foreach (var goal in _db.GetGoals())
        {
            Goals.Add(new GoalViewModel(goal));
        }

        foreach (var flag in _db.GetFlags())
        {
            Flags.Add(new FlagViewModel(flag));
        }

        foreach (var person in _db.GetPeople())
        {
            People.Add(new PersonViewModel(person));
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
                    WhoName = ResolveWhoName(card.WhoId),
                    Flags = ResolveFlags(card.FlagIds),
                    SubTasks = card.SubTasks.Select(s => new SubTaskViewModel(s)).ToList(),
                    Attachments = card.Attachments.Select(a => new AttachmentViewModel(a)).ToList(),
                    LastUpdated = card.LastUpdated
                };
                columnVm.Cards.Add(cardVm);
            }
            Columns.Add(columnVm);
        }

        RefreshProjectFilterOptions();
        RefreshWhoFilterOptions();
        RefreshGoalFilterOptions();
        RefreshFlagFilterOptions();
        ApplySort();
        RefreshDashboardStats();
    }

    public void SortByProject()
    {
        _currentSortMode = SortMode.ProjectThenDueDate;
        SelectedSortMode = "Project";
        ApplySort();
    }

    public void SortByDueDate()
    {
        _currentSortMode = SortMode.DueDateThenProject;
        SelectedSortMode = "DueDate";
        ApplySort();
    }

    public void SortByWho()
    {
        _currentSortMode = SortMode.WhoThenDueDate;
        SelectedSortMode = "Who";
        ApplySort();
    }

    public void SortByPriority()
    {
        _currentSortMode = SortMode.PriorityThenDueDate;
        SelectedSortMode = "Priority";
        ApplySort();
    }

    private static int PriorityRank(string priority) => priority switch
    {
        "High" => 0,
        "Medium" => 1,
        _ => 2
    };

    private void ApplySort()
    {
        var updates = new List<(int, int)>();

        foreach (var column in Columns)
        {
            var sorted = _currentSortMode switch
            {
                SortMode.DueDateThenProject => column.Cards.OrderBy(c => c.DueDate ?? DateTime.MaxValue).ThenBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase).ToList(),
                SortMode.WhoThenDueDate => column.Cards.OrderBy(c => c.WhoName, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.DueDate ?? DateTime.MaxValue).ToList(),
                SortMode.PriorityThenDueDate => column.Cards.OrderBy(c => PriorityRank(c.Priority)).ThenBy(c => c.DueDate ?? DateTime.MaxValue).ToList(),
                _ => column.Cards.OrderBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.DueDate ?? DateTime.MaxValue).ToList()
            };

            column.Cards.Clear();
            for (var i = 0; i < sorted.Count; i++)
            {
                column.Cards.Add(sorted[i]);
                updates.Add((sorted[i].Id, i));
            }
        }

        _db.UpdateSortOrders(updates);
    }

    private static void ReplaceFilterOptions(ObservableCollection<string> options, List<string> desired)
    {
        if (options.SequenceEqual(desired)) return;

        for (var i = options.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(options[i])) options.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (i < options.Count && options[i] == desired[i]) continue;

            var existingIndex = options.IndexOf(desired[i]);
            if (existingIndex >= 0)
            {
                options.Move(existingIndex, i);
            }
            else
            {
                options.Insert(i, desired[i]);
            }
        }
    }

    private void RefreshProjectFilterOptions()
    {
        var desired = new List<string> { "All" };
        desired.AddRange(Projects.Where(p => p.IsActive).OrderBy(p => p.Name).Select(p => p.Name));
        ReplaceFilterOptions(ProjectFilterOptions, desired);

        if (!ProjectFilterOptions.Contains(SelectedProjectFilter))
        {
            SelectedProjectFilter = "All";
        }
    }

    private void RefreshWhoFilterOptions()
    {
        var desired = new List<string> { "All", "Unassigned" };
        desired.AddRange(People.Where(p => p.IsActive).OrderBy(p => p.Name).Select(p => p.Name));
        ReplaceFilterOptions(WhoFilterOptions, desired);

        if (!WhoFilterOptions.Contains(SelectedWhoFilter))
        {
            SelectedWhoFilter = "All";
        }
    }

    private void RefreshGoalFilterOptions()
    {
        var desired = new List<string> { "All", "Unassigned" };
        desired.AddRange(Goals.Where(g => g.IsActive).OrderBy(g => g.Name).Select(g => g.Name));
        ReplaceFilterOptions(GoalFilterOptions, desired);

        if (!GoalFilterOptions.Contains(SelectedGoalFilter))
        {
            SelectedGoalFilter = "All";
        }
    }

    private void RefreshFlagFilterOptions()
    {
        var desired = new List<string> { "All", "Unassigned" };
        desired.AddRange(Flags.Where(f => f.IsActive).OrderBy(f => f.Name).Select(f => f.Name));
        ReplaceFilterOptions(FlagFilterOptions, desired);

        if (!FlagFilterOptions.Contains(SelectedFlagFilter))
        {
            SelectedFlagFilter = "All";
        }
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

        if (SelectedWhoFilter == "Unassigned")
        {
            if (card.WhoId is not null) return false;
        }
        else if (SelectedWhoFilter != "All" && card.WhoName != SelectedWhoFilter) return false;

        if (SelectedGoalFilter == "Unassigned")
        {
            if (card.GoalId is not null) return false;
        }
        else if (SelectedGoalFilter != "All" && card.GoalName != SelectedGoalFilter) return false;

        if (SelectedFlagFilter == "Unassigned")
        {
            if (card.Flags.Count > 0) return false;
        }
        else if (SelectedFlagFilter != "All" && card.Flags.All(f => f.Name != SelectedFlagFilter)) return false;

        if (DueFilter != "All")
        {
            var today = DateTime.Today;
            var matchesDue = DueFilter switch
            {
                "Today" => card.DueDate is not null && card.DueDate.Value.Date <= today,
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
                || card.WhoName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
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
        _selectedFlagFilter = "All";
        OnPropertyChanged(nameof(SelectedFlagFilter));
        _dueFilter = "All";
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

    private string ResolveWhoName(int? whoId)
    {
        if (whoId is null) return "Unassigned";
        return People.FirstOrDefault(p => p.Id == whoId)?.Name ?? "Unassigned";
    }

    private List<FlagViewModel> ResolveFlags(List<int> flagIds) =>
        Flags.Where(f => flagIds.Contains(f.Id)).ToList();

    private static void InsertSortedByName<T>(ObservableCollection<T> collection, T item, Func<T, string> nameSelector)
    {
        var name = nameSelector(item);
        var index = 0;
        while (index < collection.Count && string.Compare(nameSelector(collection[index]), name, StringComparison.OrdinalIgnoreCase) < 0)
        {
            index++;
        }
        collection.Insert(index, item);
    }

    public CardViewModel AddCard(string title, ColumnViewModel column, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, bool isImported = false, List<AttachmentViewModel>? attachments = null)
    {
        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        var card = _db.AddCard(column.Id, title.Trim(), project?.Id, column.Name, priority, dueDate, who?.Id, isRecurring, recurrencePattern, goal?.Id, notes, isImported);
        _db.SetCardFlags(card.Id, flags.Select(f => f.Id));
        var subTaskItems = _db.SetCardSubTasks(card.Id, subTasks.Select(s => (s.Title, s.IsDone)).ToList());
        var attachmentItems = _db.SetCardAttachments(card.Id, attachments.Select(a => (a.FilePath, a.DisplayName, a.AddedDate)).ToList());
        var cardVm = new CardViewModel(card)
        {
            ProjectName = project?.Name ?? "No Project",
            GoalName = goal?.Name ?? "No Goal",
            WhoName = who?.Name ?? "Unassigned",
            Flags = flags,
            SubTasks = subTaskItems.Select(s => new SubTaskViewModel(s)).ToList(),
            Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList(),
            LastUpdated = card.LastUpdated
        };
        column.Cards.Add(cardVm);

        cardVm.IsVisible = MatchesFilters(cardVm);
        ApplySort();
        RefreshDashboardStats();

        return cardVm;
    }

    public void EditCard(CardViewModel card, string title, ColumnViewModel newColumn, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, List<AttachmentViewModel>? attachments = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        var previousAttachments = card.Attachments;

        card.Title = title.Trim();
        card.ProjectId = project?.Id;
        card.ProjectName = project?.Name ?? "No Project";
        card.Priority = priority;
        card.DueDate = dueDate;
        card.WhoId = who?.Id;
        card.WhoName = who?.Name ?? "Unassigned";
        card.IsRecurring = isRecurring;
        card.RecurrencePattern = recurrencePattern;
        card.GoalId = goal?.Id;
        card.GoalName = goal?.Name ?? "No Goal";
        card.Flags = flags;
        card.Notes = notes;

        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, project?.Id, priority, dueDate, who?.Id, isRecurring, recurrencePattern, goal?.Id, notes);
        _db.SetCardFlags(card.Id, flags.Select(f => f.Id));
        var subTaskItems = _db.SetCardSubTasks(card.Id, subTasks.Select(s => (s.Title, s.IsDone)).ToList());
        card.SubTasks = subTaskItems.Select(s => new SubTaskViewModel(s)).ToList();
        var attachmentItems = _db.SetCardAttachments(card.Id, attachments.Select(a => (a.FilePath, a.DisplayName, a.AddedDate)).ToList());
        card.Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList();
        DeleteOrphanedAttachmentFiles(card.Id, previousAttachments, attachments);

        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is not null && sourceColumn != newColumn)
        {
            MoveCard(card, newColumn);
        }

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetSubTaskDone(CardViewModel card, SubTaskViewModel subTask, bool isDone)
    {
        subTask.IsDone = isDone;
        _db.SetSubTaskDone(subTask.Id, isDone);
        card.RefreshSubTaskProgress();
    }

    public void AddFlagToCard(CardViewModel card, FlagViewModel flag)
    {
        if (card.Flags.Any(f => f.Id == flag.Id)) return;

        card.Flags = card.Flags.Append(flag).ToList();
        _db.SetCardFlags(card.Id, card.Flags.Select(f => f.Id));
    }

    public void SetCardPriority(CardViewModel card, string priority)
    {
        if (card.Priority == priority) return;

        card.Priority = priority;
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, priority, card.DueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardWho(CardViewModel card, PersonViewModel? who)
    {
        if (card.WhoId == who?.Id) return;

        card.WhoId = who?.Id;
        card.WhoName = who?.Name ?? "Unassigned";
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, card.Priority, card.DueDate, who?.Id,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    private void DeleteOrphanedAttachmentFiles(int cardId, List<AttachmentViewModel> previousAttachments, List<AttachmentViewModel> newAttachments)
    {
        var attachmentsDir = Path.GetFullPath(AttachmentsDir);
        var removed = previousAttachments.Where(old => !newAttachments.Any(a => a.Id != 0 && a.Id == old.Id));

        foreach (var attachment in removed)
        {
            try
            {
                var fullPath = Path.GetFullPath(attachment.FilePath);
                if (!fullPath.StartsWith(attachmentsDir, StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(fullPath)) continue;
                if (_db.IsAttachmentPathReferencedElsewhere(fullPath, cardId)) continue;

                File.Delete(fullPath);
            }
            catch
            {
                // Best-effort cleanup; leave the file if it can't be removed.
            }
        }
    }

    public void AddProject(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var project = _db.AddProject(name.Trim());
        InsertSortedByName(Projects, new ProjectViewModel(project), p => p.Name);
        RefreshProjectFilterOptions();
    }

    public void RenameProject(ProjectViewModel project, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || project.Name == newName.Trim()) return;

        project.Name = newName.Trim();
        _db.RenameProject(project.Id, project.Name);
        Projects.Remove(project);
        InsertSortedByName(Projects, project, p => p.Name);

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

    public void SetProjectActive(ProjectViewModel project, bool isActive)
    {
        if (project.IsActive == isActive) return;

        project.IsActive = isActive;
        _db.SetProjectActive(project.Id, isActive);
        RefreshProjectFilterOptions();
        OnPropertyChanged(nameof(ActiveProjects));
    }

    public int CountTasksUsingProject(ProjectViewModel project) =>
        Columns.SelectMany(c => c.Cards).Count(c => c.ProjectId == project.Id);

    public void AddPerson(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var person = _db.AddPerson(name.Trim());
        InsertSortedByName(People, new PersonViewModel(person), p => p.Name);
        RefreshWhoFilterOptions();
    }

    public void RenamePerson(PersonViewModel person, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || person.Name == newName.Trim()) return;

        person.Name = newName.Trim();
        _db.RenamePerson(person.Id, person.Name);
        People.Remove(person);
        InsertSortedByName(People, person, p => p.Name);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.WhoId == person.Id))
        {
            card.WhoName = person.Name;
        }

        RefreshWhoFilterOptions();
    }

    public void DeletePerson(PersonViewModel person)
    {
        _db.DeletePerson(person.Id);
        People.Remove(person);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.WhoId == person.Id))
        {
            card.WhoId = null;
            card.WhoName = "Unassigned";
        }

        RefreshWhoFilterOptions();
    }

    public void SetPersonActive(PersonViewModel person, bool isActive)
    {
        if (person.IsActive == isActive) return;

        person.IsActive = isActive;
        _db.SetPersonActive(person.Id, isActive);
        RefreshWhoFilterOptions();
        OnPropertyChanged(nameof(ActivePeople));
    }

    public int CountTasksUsingPerson(PersonViewModel person) =>
        Columns.SelectMany(c => c.Cards).Count(c => c.WhoId == person.Id);

    public void AddGoal(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var goal = _db.AddGoal(name.Trim());
        InsertSortedByName(Goals, new GoalViewModel(goal), g => g.Name);
        RefreshGoalFilterOptions();
    }

    public void RenameGoal(GoalViewModel goal, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || goal.Name == newName.Trim()) return;

        goal.Name = newName.Trim();
        _db.RenameGoal(goal.Id, goal.Name);
        Goals.Remove(goal);
        InsertSortedByName(Goals, goal, g => g.Name);

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

    public void SetGoalActive(GoalViewModel goal, bool isActive)
    {
        if (goal.IsActive == isActive) return;

        goal.IsActive = isActive;
        _db.SetGoalActive(goal.Id, isActive);
        RefreshGoalFilterOptions();
        OnPropertyChanged(nameof(ActiveGoals));
    }

    public int CountTasksUsingGoal(GoalViewModel goal) =>
        Columns.SelectMany(c => c.Cards).Count(c => c.GoalId == goal.Id);

    public void AddFlag(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var flag = _db.AddFlag(name.Trim());
        InsertSortedByName(Flags, new FlagViewModel(flag), f => f.Name);
        RefreshFlagFilterOptions();
    }

    public void RenameFlag(FlagViewModel flag, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || flag.Name == newName.Trim()) return;

        flag.Name = newName.Trim();
        _db.RenameFlag(flag.Id, flag.Name);
        Flags.Remove(flag);
        InsertSortedByName(Flags, flag, f => f.Name);
        RefreshFlagFilterOptions();
    }

    public void DeleteFlag(FlagViewModel flag)
    {
        _db.DeleteFlag(flag.Id);
        Flags.Remove(flag);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.Flags.Contains(flag)))
        {
            card.Flags = card.Flags.Where(f => f.Id != flag.Id).ToList();
        }

        RefreshFlagFilterOptions();
    }

    public void SetFlagActive(FlagViewModel flag, bool isActive)
    {
        if (flag.IsActive == isActive) return;

        flag.IsActive = isActive;
        _db.SetFlagActive(flag.Id, isActive);
        RefreshFlagFilterOptions();
        OnPropertyChanged(nameof(ActiveFlags));
    }

    public int CountTasksUsingFlag(FlagViewModel flag) =>
        Columns.SelectMany(c => c.Cards).Count(c => c.Flags.Contains(flag));

    public List<DeletedCardInfo> GetDeletedCards() => _db.GetDeletedCards();

    public void ReactivateCard(int cardId, string cardTitle)
    {
        _db.ReactivateCard(cardId, cardTitle);
        Load();
        RefreshDashboardStats();
    }

    private void DeleteCard(CardViewModel? card)
    {
        if (card is null) return;

        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        column?.Cards.Remove(card);
        _db.DeleteCard(card.Id, card.Title, column?.Name ?? "Unknown");
        RefreshDashboardStats();
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
        RefreshDashboardStats();
    }

    private void SpawnNextOccurrence(CardViewModel completedCard)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do");
        if (toDoColumn is null) return;

        var nextDueDate = CalculateNextDueDate(completedCard.DueDate ?? DateTime.Today, completedCard.RecurrencePattern!);
        var project = Projects.FirstOrDefault(p => p.Id == completedCard.ProjectId);
        var goal = Goals.FirstOrDefault(g => g.Id == completedCard.GoalId);
        var who = People.FirstOrDefault(p => p.Id == completedCard.WhoId);
        var freshSubTasks = completedCard.SubTasks
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = false }))
            .ToList();

        AddCard(completedCard.Title, toDoColumn, project, completedCard.Priority, nextDueDate, who,
            true, completedCard.RecurrencePattern, goal, completedCard.Flags, freshSubTasks, completedCard.Notes);
    }

    private static DateTime CalculateNextDueDate(DateTime anchor, string pattern) => pattern switch
    {
        "Daily" => anchor.AddDays(1),
        "Weekday" => NextWeekday(anchor),
        "Weekly" => anchor.AddDays(7),
        "Bi-Weekly" => anchor.AddDays(14),
        "Monthly" => anchor.AddMonths(1),
        "Bi-Monthly" => anchor.AddMonths(2),
        "Quarterly" => anchor.AddMonths(3),
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

        RefreshDashboardStats();
    }

    public List<ArchivedCardInfo> GetArchivedCards() => _db.GetArchivedCards();

    public List<CardViewModel> GetImportedCards() =>
        Columns.SelectMany(c => c.Cards).Where(c => c.IsImported).ToList();

    public void SetCardImported(CardViewModel card, bool isImported)
    {
        if (card.IsImported == isImported) return;

        card.IsImported = isImported;
        _db.SetCardImported(card.Id, isImported);
    }

    public List<CardViewModel> ImportCards(IEnumerable<ImportedTaskRow> rows)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do") ?? Columns.First();
        var created = new List<CardViewModel>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Title)) continue;

            var column = toDoColumn;
            if (!string.IsNullOrWhiteSpace(row.Category))
            {
                var match = Columns.FirstOrDefault(c => string.Equals(c.Name, row.Category.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match is not null) column = match;
            }

            var priority = row.Priority?.Trim() switch
            {
                { } p when string.Equals(p, "High", StringComparison.OrdinalIgnoreCase) => "High",
                { } p when string.Equals(p, "Medium", StringComparison.OrdinalIgnoreCase) => "Medium",
                _ => "Normal"
            };

            ProjectViewModel? project = null;
            if (!string.IsNullOrWhiteSpace(row.Project))
            {
                var name = row.Project.Trim();
                project = Projects.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (project is null)
                {
                    AddProject(name);
                    project = Projects.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            GoalViewModel? goal = null;
            if (!string.IsNullOrWhiteSpace(row.Goal))
            {
                var name = row.Goal.Trim();
                goal = Goals.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
                if (goal is null)
                {
                    AddGoal(name);
                    goal = Goals.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            PersonViewModel? who = null;
            if (!string.IsNullOrWhiteSpace(row.Who))
            {
                var name = row.Who.Trim();
                who = People.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (who is null)
                {
                    AddPerson(name);
                    who = People.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            var cardVm = AddCard(row.Title.Trim(), column, project, priority, row.DueDate, who,
                false, null, goal, isImported: true);
            created.Add(cardVm);
        }

        return created;
    }
}
