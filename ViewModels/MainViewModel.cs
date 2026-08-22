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

    public enum SortKey { Project, DueDate, Who, Priority }

    // Order matters: this is the active multi-key sort, most-significant key first. Never empty -
    // ToggleSortKey falls back to the default rather than letting the board end up unsorted.
    private readonly List<SortKey> _sortKeys = [SortKey.Project];

    public int ProjectSortRank => SortRankOf(SortKey.Project);
    public int DueDateSortRank => SortRankOf(SortKey.DueDate);
    public int WhoSortRank => SortRankOf(SortKey.Who);
    public int PrioritySortRank => SortRankOf(SortKey.Priority);

    private int SortRankOf(SortKey key)
    {
        var index = _sortKeys.IndexOf(key);
        return index < 0 ? 0 : index + 1;
    }

    private void NotifySortRanksChanged()
    {
        OnPropertyChanged(nameof(ProjectSortRank));
        OnPropertyChanged(nameof(DueDateSortRank));
        OnPropertyChanged(nameof(WhoSortRank));
        OnPropertyChanged(nameof(PrioritySortRank));
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
    public bool ShowDueReminders { get; private set; } = true;
    public bool RememberLastView { get; private set; }

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

    public void SetShowDueReminders(bool value)
    {
        ShowDueReminders = value;
        _db.SetSetting("ShowDueReminders", value ? "True" : "False");
    }

    public void SetRememberLastView(bool value)
    {
        RememberLastView = value;
        _db.SetSetting("RememberLastView", value ? "True" : "False");
    }

    public void SaveLastViewState()
    {
        if (!RememberLastView) return;

        _db.SetSetting("LastProjectFilter", SelectedProjectFilter);
        _db.SetSetting("LastPriorityFilter", SelectedPriorityFilter);
        _db.SetSetting("LastWhoFilter", SelectedWhoFilter);
        _db.SetSetting("LastGoalFilter", SelectedGoalFilter);
        _db.SetSetting("LastFlagFilter", SelectedFlagFilter);
        _db.SetSetting("LastDueFilter", DueFilter);
        _db.SetSetting("LastDueRangeFrom", DueRangeFrom?.ToString("yyyy-MM-dd") ?? string.Empty);
        _db.SetSetting("LastDueRangeTo", DueRangeTo?.ToString("yyyy-MM-dd") ?? string.Empty);
        _db.SetSetting("LastKeywordFilter", KeywordFilter);
        _db.SetSetting("LastSortMode", string.Join(",", _sortKeys));
    }

    private static readonly Brush[] ColumnPaletteLight =
    [
        new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xEF)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xCD)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0xE1, 0xD5, 0xF5)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA)), // Done - green
    ];

    private static readonly Brush[] ColumnPaletteDark =
    [
        new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x38)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0x4D, 0x42, 0x20)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0x4D, 0x34, 0x19)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0x3B, 0x2F, 0x4D)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0x20, 0x40, 0x30)), // Done - green
    ];

    public int OpenTaskCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards).Count();

    public int OverdueCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate is not null && c.DueDate.Value.Date < DateTime.Today);

    public int DueTodayCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate?.Date == DateTime.Today);

    public int DueThisWeekCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate is not null && c.DueDate.Value.Date >= DateTime.Today && c.DueDate.Value.Date <= DateTime.Today.AddDays(7));

    public List<(CardViewModel Card, string ColumnName)> GetArchivedReportRows()
    {
        var displayNameById = Columns.ToDictionary(c => c.Id, c => c.DisplayName);

        return _db.GetCards(archivedOnly: true).Select(card =>
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
            var columnName = displayNameById.GetValueOrDefault(card.ColumnId, "Unknown");
            return (cardVm, columnName);
        }).ToList();
    }

    public List<CardViewModel> GetDueReminders() =>
        Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
            .Where(c => c.DueDate is not null && c.DueDate.Value.Date <= DateTime.Today)
            .OrderBy(c => c.DueDate)
            .ThenBy(c => PriorityRank(c.Priority))
            .ThenBy(c => c.Title)
            .ToList();

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
    public ObservableCollection<string> PriorityFilterOptions { get; } = ["All", "High", "Medium", "Normal", "Low"];
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
        set
        {
            if (!SetField(ref _dueFilter, value ?? "All")) return;
            ClearDueRange(notify: true);
            ApplyFilters();
        }
    }

    private DateTime? _dueRangeFrom;
    public DateTime? DueRangeFrom
    {
        get => _dueRangeFrom;
        set
        {
            if (!SetField(ref _dueRangeFrom, value)) return;
            if (_dueFilter != "All") { _dueFilter = "All"; OnPropertyChanged(nameof(DueFilter)); }
            ApplyFilters();
        }
    }

    private DateTime? _dueRangeTo;
    public DateTime? DueRangeTo
    {
        get => _dueRangeTo;
        set
        {
            if (!SetField(ref _dueRangeTo, value)) return;
            if (_dueFilter != "All") { _dueFilter = "All"; OnPropertyChanged(nameof(DueFilter)); }
            ApplyFilters();
        }
    }

    private void ClearDueRange(bool notify)
    {
        if (_dueRangeFrom is not null)
        {
            _dueRangeFrom = null;
            if (notify) OnPropertyChanged(nameof(DueRangeFrom));
        }
        if (_dueRangeTo is not null)
        {
            _dueRangeTo = null;
            if (notify) OnPropertyChanged(nameof(DueRangeTo));
        }
    }

    private string _keywordFilter = string.Empty;
    public string KeywordFilter
    {
        get => _keywordFilter;
        set { if (SetField(ref _keywordFilter, value)) ApplyFilters(); }
    }

    public RelayCommand DeleteCardCommand { get; }
    public RelayCommand MoveCardCommand { get; }

    private readonly ManagedList<Project, ProjectViewModel> _projectManager;
    private readonly ManagedList<Person, PersonViewModel> _personManager;
    private readonly ManagedList<Goal, GoalViewModel> _goalManager;
    private readonly ManagedList<Flag, FlagViewModel> _flagManager;

    public MainViewModel(DatabaseService db)
    {
        _db = db;
        RememberLastView = _db.GetSetting("RememberLastView") == "True";

        _projectManager = new ManagedList<Project, ProjectViewModel>(
            Projects, _db.AddProject, _db.RenameProject, _db.DeleteProject, _db.SetProjectActive, m => new ProjectViewModel(m),
            RefreshProjectFilterOptions, () => OnPropertyChanged(nameof(ActiveProjects)),
            item => UpdateMatchingCards(c => c.ProjectId == item.Id, c => c.ProjectName = item.Name),
            item => UpdateMatchingCards(c => c.ProjectId == item.Id, c => { c.ProjectId = null; c.ProjectName = "No Project"; }),
            item => Columns.SelectMany(c => c.Cards).Count(c => c.ProjectId == item.Id));

        _personManager = new ManagedList<Person, PersonViewModel>(
            People, _db.AddPerson, _db.RenamePerson, _db.DeletePerson, _db.SetPersonActive, m => new PersonViewModel(m),
            RefreshWhoFilterOptions, () => OnPropertyChanged(nameof(ActivePeople)),
            item => UpdateMatchingCards(c => c.WhoId == item.Id, c => c.WhoName = item.Name),
            item => UpdateMatchingCards(c => c.WhoId == item.Id, c => { c.WhoId = null; c.WhoName = "Unassigned"; }),
            item => Columns.SelectMany(c => c.Cards).Count(c => c.WhoId == item.Id));

        _goalManager = new ManagedList<Goal, GoalViewModel>(
            Goals, _db.AddGoal, _db.RenameGoal, _db.DeleteGoal, _db.SetGoalActive, m => new GoalViewModel(m),
            RefreshGoalFilterOptions, () => OnPropertyChanged(nameof(ActiveGoals)),
            item => UpdateMatchingCards(c => c.GoalId == item.Id, c => c.GoalName = item.Name),
            item => UpdateMatchingCards(c => c.GoalId == item.Id, c => { c.GoalId = null; c.GoalName = "No Goal"; }),
            item => Columns.SelectMany(c => c.Cards).Count(c => c.GoalId == item.Id));

        _flagManager = new ManagedList<Flag, FlagViewModel>(
            Flags, _db.AddFlag, _db.RenameFlag, _db.DeleteFlag, _db.SetFlagActive, m => new FlagViewModel(m),
            RefreshFlagFilterOptions, () => OnPropertyChanged(nameof(ActiveFlags)),
            _ => { }, // Flags are shared object references in each card's Flags list, so a rename needs no per-card sync.
            item => UpdateMatchingCards(c => c.Flags.Contains(item), c => c.Flags = c.Flags.Where(f => f.Id != item.Id).ToList()),
            item => Columns.SelectMany(c => c.Cards).Count(c => c.Flags.Contains(item)));

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
        ApplyColumnPalette();

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
        ShowDueReminders = _db.GetSetting("ShowDueReminders") != "False";
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
        ApplyColumnPalette();
    }

    private void ApplyColumnPalette()
    {
        var palette = IsDarkMode ? ColumnPaletteDark : ColumnPaletteLight;
        for (var i = 0; i < Columns.Count; i++)
        {
            Columns[i].Background = palette[i % palette.Length];
        }
    }

    public void ToggleCardSize()
    {
        IsCompactCards = !IsCompactCards;
        _db.SetSetting("CardSize", IsCompactCards ? "Compact" : "Large");
    }

    public void RenameColumnDisplayName(ColumnViewModel column, string newDisplayName)
    {
        var trimmed = newDisplayName.Trim();
        if (trimmed.Length == 0 || column.DisplayName == trimmed) return;

        column.DisplayName = trimmed;
        _db.RenameColumnDisplayName(column.Id, trimmed);
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
            var palette = IsDarkMode ? ColumnPaletteDark : ColumnPaletteLight;
            var background = palette[i % palette.Length];
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

        if (RememberLastView)
        {
            _selectedProjectFilter = _db.GetSetting("LastProjectFilter") ?? "All";
            _selectedPriorityFilter = _db.GetSetting("LastPriorityFilter") ?? "All";
            _selectedWhoFilter = _db.GetSetting("LastWhoFilter") ?? "All";
            _selectedGoalFilter = _db.GetSetting("LastGoalFilter") ?? "All";
            _selectedFlagFilter = _db.GetSetting("LastFlagFilter") ?? "All";
            _dueFilter = _db.GetSetting("LastDueFilter") ?? "All";
            _dueRangeFrom = DateTime.TryParse(_db.GetSetting("LastDueRangeFrom"), out var savedFrom) ? savedFrom : null;
            _dueRangeTo = DateTime.TryParse(_db.GetSetting("LastDueRangeTo"), out var savedTo) ? savedTo : null;
            _keywordFilter = _db.GetSetting("LastKeywordFilter") ?? string.Empty;
            var savedKeys = (_db.GetSetting("LastSortMode") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => Enum.TryParse<SortKey>(token, out var key) ? key : (SortKey?)null)
                .Where(key => key is not null)
                .Select(key => key!.Value)
                .Distinct()
                .ToList();

            if (savedKeys.Count > 0)
            {
                _sortKeys.Clear();
                _sortKeys.AddRange(savedKeys);
            }
        }

        RefreshProjectFilterOptions();
        RefreshWhoFilterOptions();
        RefreshGoalFilterOptions();
        RefreshFlagFilterOptions();
        ApplyFilters();
        ApplySort();
        RefreshDashboardStats();
    }

    // Plain click: reset the sort to just this one key. Ctrl+click: toggle this key in/out of the
    // active multi-key sort, appending it at the end when added. Never leaves the sort empty - if
    // toggling off the last key would do that, it falls back to the default (Project alone).
    public void ToggleSortKey(SortKey key, bool additive)
    {
        if (additive)
        {
            if (!_sortKeys.Remove(key))
            {
                _sortKeys.Add(key);
            }

            if (_sortKeys.Count == 0)
            {
                _sortKeys.Add(SortKey.Project);
            }
        }
        else
        {
            _sortKeys.Clear();
            _sortKeys.Add(key);
        }

        NotifySortRanksChanged();
        ApplySort();
    }

    private static int PriorityRank(string priority) => priority switch
    {
        "High" => 0,
        "Medium" => 1,
        "Normal" => 2,
        "Low" => 3,
        _ => 2
    };

    private static IOrderedEnumerable<CardViewModel> OrderByKey(IEnumerable<CardViewModel> cards, SortKey key) => key switch
    {
        SortKey.DueDate => cards.OrderBy(c => c.DueDate ?? DateTime.MaxValue),
        SortKey.Who => cards.OrderBy(c => c.WhoName, StringComparer.OrdinalIgnoreCase),
        SortKey.Priority => cards.OrderBy(c => PriorityRank(c.Priority)),
        _ => cards.OrderBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase)
    };

    private static IOrderedEnumerable<CardViewModel> ThenByKey(IOrderedEnumerable<CardViewModel> cards, SortKey key) => key switch
    {
        SortKey.DueDate => cards.ThenBy(c => c.DueDate ?? DateTime.MaxValue),
        SortKey.Who => cards.ThenBy(c => c.WhoName, StringComparer.OrdinalIgnoreCase),
        SortKey.Priority => cards.ThenBy(c => PriorityRank(c.Priority)),
        _ => cards.ThenBy(c => c.ProjectName, StringComparer.OrdinalIgnoreCase)
    };

    private void ApplySort()
    {
        var updates = new List<(int, int)>();

        foreach (var column in Columns)
        {
            var ordered = OrderByKey(column.Cards, _sortKeys[0]);
            for (var k = 1; k < _sortKeys.Count; k++)
            {
                ordered = ThenByKey(ordered, _sortKeys[k]);
            }
            var sorted = ordered.ToList();

            // Reorder in place with Move() rather than Clear()+Add(): the latter tears down and
            // recreates every card's visual container, which can orphan an in-flight drag capture
            // or a still-closing popup anchored to one of those cards and appear to freeze the app.
            for (var i = 0; i < sorted.Count; i++)
            {
                var currentIndex = column.Cards.IndexOf(sorted[i]);
                if (currentIndex != i)
                {
                    column.Cards.Move(currentIndex, i);
                }
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
                "Within a Week" => card.DueDate is not null && card.DueDate.Value.Date <= today.AddDays(7),
                "No Due Date" => card.DueDate is null,
                _ => true
            };
            if (!matchesDue) return false;
        }

        if (DueRangeFrom is not null || DueRangeTo is not null)
        {
            if (card.DueDate is null) return false;
            if (DueRangeFrom is not null && card.DueDate.Value.Date < DueRangeFrom.Value.Date) return false;
            if (DueRangeTo is not null && card.DueDate.Value.Date > DueRangeTo.Value.Date) return false;
        }

        if (!string.IsNullOrWhiteSpace(KeywordFilter))
        {
            var keyword = KeywordFilter.Trim();
            var matchesKeyword = card.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || card.ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || card.WhoName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (card.Notes?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
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
        ClearDueRange(notify: true);
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

    private void UpdateMatchingCards(Func<CardViewModel, bool> predicate, Action<CardViewModel> update)
    {
        foreach (var card in Columns.SelectMany(c => c.Cards).Where(predicate))
        {
            update(card);
        }
    }

    public CardViewModel AddCard(string title, ColumnViewModel column, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, bool isImported = false, List<AttachmentViewModel>? attachments = null, bool forceEditOnComplete = false)
    {
        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        var card = _db.AddCard(column.Id, title.Trim(), project?.Id, column.Name, priority, dueDate, who?.Id, isRecurring, recurrencePattern, goal?.Id, notes, isImported, forceEditOnComplete);
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
        string? notes = null, List<AttachmentViewModel>? attachments = null, bool forceEditOnComplete = false)
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
        card.ForceEditOnComplete = forceEditOnComplete;

        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, project?.Id, priority, dueDate, who?.Id, isRecurring, recurrencePattern, goal?.Id, notes, forceEditOnComplete);
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

    public void AddAttachmentToCard(CardViewModel card, string filePath, string displayName)
    {
        var updatedAttachments = card.Attachments
            .Select(a => (a.FilePath, a.DisplayName, a.AddedDate))
            .Append((filePath, displayName, DateTime.Now))
            .ToList();

        var attachmentItems = _db.SetCardAttachments(card.Id, updatedAttachments);
        card.Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList();
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, card.Priority, card.DueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);
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
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardDueDate(CardViewModel card, DateTime? dueDate)
    {
        if (card.DueDate == dueDate) return;

        card.DueDate = dueDate;
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, card.Priority, dueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

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
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardProject(CardViewModel card, ProjectViewModel project)
    {
        if (card.ProjectId == project.Id) return;

        card.ProjectId = project.Id;
        card.ProjectName = project.Name;
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, project.Id, card.Priority, card.DueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    /// <summary>
    /// Moves a card's attachment files (screenshots and linked files alike) into the Attachments
    /// folder's Done/Archived/Deleted subfolder to match its current status, or back to the
    /// Attachments folder root when it's none of those. Skips a file still referenced by another
    /// card (so that card's link never breaks) and any file that's gone missing on disk.
    /// </summary>
    private void ReconcileAttachmentLocations(CardViewModel card, string? statusSubfolder)
    {
        if (card.Attachments.Count == 0) return;

        var destinationDir = string.IsNullOrEmpty(statusSubfolder) ? AttachmentsDir : Path.Combine(AttachmentsDir, statusSubfolder);
        var updated = new List<(string FilePath, string DisplayName, DateTime AddedDate)>();
        var changed = false;

        foreach (var attachment in card.Attachments)
        {
            var currentPath = attachment.FilePath;

            if (!File.Exists(currentPath) || _db.IsAttachmentPathReferencedElsewhere(currentPath, card.Id))
            {
                updated.Add((attachment.FilePath, attachment.DisplayName, attachment.AddedDate));
                continue;
            }

            try
            {
                var newPath = MoveAttachmentFile(currentPath, destinationDir);
                if (!string.Equals(newPath, currentPath, StringComparison.OrdinalIgnoreCase)) changed = true;
                updated.Add((newPath, attachment.DisplayName, attachment.AddedDate));
            }
            catch
            {
                // Best-effort: if the move fails (e.g. file in use), keep the existing reference rather than losing it.
                updated.Add((attachment.FilePath, attachment.DisplayName, attachment.AddedDate));
            }
        }

        if (!changed) return;

        var attachmentItems = _db.SetCardAttachments(card.Id, updated);
        card.Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList();
    }

    private static string MoveAttachmentFile(string currentPath, string destinationDir)
    {
        var destPath = Path.Combine(destinationDir, Path.GetFileName(currentPath));
        if (string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
        {
            return currentPath;
        }

        Directory.CreateDirectory(destinationDir);

        if (File.Exists(destPath))
        {
            var nameOnly = Path.GetFileNameWithoutExtension(destPath);
            var ext = Path.GetExtension(destPath);
            var counter = 1;
            do
            {
                destPath = Path.Combine(destinationDir, $"{nameOnly}_{counter}{ext}");
                counter++;
            } while (File.Exists(destPath));
        }

        File.Move(currentPath, destPath);
        return destPath;
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

    public void AddProject(string name) => _projectManager.Add(name);
    public void RenameProject(ProjectViewModel project, string newName) => _projectManager.Rename(project, newName);
    public void DeleteProject(ProjectViewModel project) => _projectManager.Delete(project);
    public void SetProjectActive(ProjectViewModel project, bool isActive) => _projectManager.SetActive(project, isActive);
    public int CountTasksUsingProject(ProjectViewModel project) => _projectManager.CountUsage(project);

    public void AddPerson(string name) => _personManager.Add(name);
    public void RenamePerson(PersonViewModel person, string newName) => _personManager.Rename(person, newName);
    public void DeletePerson(PersonViewModel person) => _personManager.Delete(person);
    public void SetPersonActive(PersonViewModel person, bool isActive) => _personManager.SetActive(person, isActive);
    public int CountTasksUsingPerson(PersonViewModel person) => _personManager.CountUsage(person);

    public void AddGoal(string name) => _goalManager.Add(name);
    public void RenameGoal(GoalViewModel goal, string newName) => _goalManager.Rename(goal, newName);
    public void DeleteGoal(GoalViewModel goal) => _goalManager.Delete(goal);
    public void SetGoalActive(GoalViewModel goal, bool isActive) => _goalManager.SetActive(goal, isActive);
    public int CountTasksUsingGoal(GoalViewModel goal) => _goalManager.CountUsage(goal);

    public void AddFlag(string name) => _flagManager.Add(name);
    public void RenameFlag(FlagViewModel flag, string newName) => _flagManager.Rename(flag, newName);
    public void DeleteFlag(FlagViewModel flag) => _flagManager.Delete(flag);
    public void SetFlagActive(FlagViewModel flag, bool isActive) => _flagManager.SetActive(flag, isActive);
    public int CountTasksUsingFlag(FlagViewModel flag) => _flagManager.CountUsage(flag);

    public List<DeletedCardInfo> GetDeletedCards() => _db.GetDeletedCards();

    public void ReactivateCard(int cardId, string cardTitle)
    {
        _db.ReactivateCard(cardId, cardTitle);
        Load();
        RefreshDashboardStats();

        var reactivatedCard = Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Id == cardId);
        if (reactivatedCard is not null) ReconcileAttachmentLocations(reactivatedCard, null);
    }

    private void DeleteCard(CardViewModel? card)
    {
        if (card is null) return;

        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        ReconcileAttachmentLocations(card, "Deleted");
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
        ReconcileAttachmentLocations(card, targetColumn.Name == "Done" ? "Done" : null);

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
            true, completedCard.RecurrencePattern, goal, completedCard.Flags, freshSubTasks, completedCard.Notes,
            forceEditOnComplete: completedCard.ForceEditOnComplete);
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
            ReconcileAttachmentLocations(card, "Archived");
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
                var match = Columns.FirstOrDefault(c => string.Equals(c.DisplayName, row.Category.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match is not null) column = match;
            }

            var priority = row.Priority?.Trim() switch
            {
                { } p when string.Equals(p, "High", StringComparison.OrdinalIgnoreCase) => "High",
                { } p when string.Equals(p, "Medium", StringComparison.OrdinalIgnoreCase) => "Medium",
                { } p when string.Equals(p, "Low", StringComparison.OrdinalIgnoreCase) => "Low",
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
