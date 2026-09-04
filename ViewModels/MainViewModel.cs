using System.Collections.ObjectModel;
using System.Reflection;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// Split across multiple files by concern (see MainViewModel.*.cs) since this class is the app's
// central view model and had grown too large to navigate as a single file. This file holds shared
// state (the collections, the constructor, Load()) and small cross-cutting helpers; everything
// else - Settings, Dashboard, Sorting, Filters, Cards, Attachments, Recurring, ArchiveDelete,
// ManagedLists, Import - lives in its own partial file.
public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public string AppVersion { get; } = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public string CopyrightText { get; } = AppInfo.Copyright;
    public bool IsTestChannel => AppChannel.IsTest;
    public string WindowTitle => AppChannel.DisplayName;

    public string CurrentDbPath => _db.DbPath;
    public string AttachmentsDir => DatabaseService.GetAttachmentsDir(_db.DbPath);

    public ObservableCollection<ColumnViewModel> Columns { get; } = [];
    public ObservableCollection<ProjectViewModel> Projects { get; } = [];
    public ObservableCollection<GoalViewModel> Goals { get; } = [];
    public ObservableCollection<FlagViewModel> Flags { get; } = [];
    public ObservableCollection<PersonViewModel> People { get; } = [];

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

        UserName = _db.GetSetting("UserName") ?? string.Empty;
        UserTitle = _db.GetSetting("UserTitle") ?? string.Empty;
        UserEmail = _db.GetSetting("UserEmail") ?? string.Empty;
        UserPhone = _db.GetSetting("UserPhone") ?? string.Empty;

        StartFullScreen = _db.GetSetting("StartFullScreen") == "True";
        ConfirmDelete = _db.GetSetting("ConfirmDelete") != "False";
        ConfirmArchive = _db.GetSetting("ConfirmArchive") != "False";
        AddNoteOnComplete = _db.GetSetting("AddNoteOnComplete") == "True";
        ShowDueReminders = _db.GetSetting("ShowDueReminders") != "False";
        ShowWhatsNew = _db.GetSetting("ShowWhatsNew") != "False";
        LoadCustomFilters();
        LoadSavedReportViews();
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
                    WhoEmail = ResolveWhoEmail(card.WhoId),
                    Flags = ResolveFlags(card.FlagIds),
                    SubTasks = card.SubTasks.Select(s => new SubTaskViewModel(s)).ToList(),
                    Attachments = card.Attachments.Select(a => new AttachmentViewModel(a)).ToList(),
                    LastUpdated = card.LastUpdated
                };
                columnVm.Cards.Add(cardVm);
            }
            Columns.Add(columnVm);
        }

        List<string>? savedProjectFilter = null;
        List<string>? savedPriorityFilter = null;
        List<string>? savedWhoFilter = null;

        if (RememberLastView)
        {
            savedProjectFilter = SplitFilterNames(_db.GetSetting("LastProjectFilter"));
            savedPriorityFilter = SplitFilterNames(_db.GetSetting("LastPriorityFilter"));
            savedWhoFilter = SplitFilterNames(_db.GetSetting("LastWhoFilter"));
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

        // Applied after the options lists are populated, since restoring a selection means setting
        // IsSelected on the actual FilterOptionViewModel instances, not a bare string. A saved name
        // ("All" from before multi-select existed included) that isn't an option name is dropped -
        // "All" split alone naturally selects nothing, which is exactly what it always meant.
        if (savedProjectFilter is not null) ApplySelection(ProjectFilterOptions, savedProjectFilter);
        if (savedPriorityFilter is not null) ApplySelection(PriorityFilterOptions, savedPriorityFilter);
        if (savedWhoFilter is not null) ApplySelection(WhoFilterOptions, savedWhoFilter);

        ApplyFilters();
        ApplySort();
        RefreshDashboardStats();
    }

    private static List<string> SplitFilterNames(string? raw) =>
        string.IsNullOrEmpty(raw) ? [] : raw.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

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

    private string? ResolveWhoEmail(int? whoId)
    {
        if (whoId is null) return null;
        return People.FirstOrDefault(p => p.Id == whoId)?.Email;
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
}
