using System.Collections.ObjectModel;

namespace KanbanApp.ViewModels;

// The sidebar's filter dropdowns/keyword box/due-date range: option lists, selected values, and
// the MatchesFilters predicate that drives each card's IsVisible.
public partial class MainViewModel
{
    // Project/Priority/Who are multi-select (Ctrl/Shift-click, via FilterOptionViewModel.IsSelected
    // bound to each ListBoxItem) - no selection means no restriction on that field, same meaning
    // "All" had as a single-select value. Goal/Flag/Due stay single-select ComboBoxes.
    public ObservableCollection<FilterOptionViewModel> ProjectFilterOptions { get; } = [];
    public ObservableCollection<FilterOptionViewModel> PriorityFilterOptions { get; } =
        [new("High"), new("Medium"), new("Normal"), new("Low")];
    public ObservableCollection<FilterOptionViewModel> WhoFilterOptions { get; } = [];

    public ObservableCollection<string> GoalFilterOptions { get; } = ["All"];
    public ObservableCollection<string> FlagFilterOptions { get; } = ["All"];
    public ObservableCollection<string> DueFilterOptions { get; } = ["All", "Today", "Tomorrow", "Within a Week", "No Due Date"];

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

    // Same reorder-in-place approach as ReplaceFilterOptions, but for the multi-select lists: a
    // surviving option's IsSelected state (and the FilterOptionViewModel instance itself, since
    // that's what ListBox.SelectedItems tracks) carries over across a refresh; a name that's gone
    // (project renamed away, person deactivated) just drops out along with whatever selection it had.
    private static void SyncFilterOptions(ObservableCollection<FilterOptionViewModel> options, List<string> desired)
    {
        for (var i = options.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(options[i].Name)) options.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (i < options.Count && options[i].Name == desired[i]) continue;

            var existingIndex = -1;
            for (var j = 0; j < options.Count; j++)
            {
                if (options[j].Name == desired[i]) { existingIndex = j; break; }
            }

            if (existingIndex >= 0)
            {
                options.Move(existingIndex, i);
            }
            else
            {
                options.Insert(i, new FilterOptionViewModel(desired[i]));
            }
        }
    }

    private void RefreshProjectFilterOptions()
    {
        var desired = Projects.Where(p => p.IsActive).OrderBy(p => p.Name).Select(p => p.Name).ToList();
        SyncFilterOptions(ProjectFilterOptions, desired);
    }

    private void RefreshWhoFilterOptions()
    {
        var desired = new List<string> { "Unassigned" };
        desired.AddRange(People.Where(p => p.IsActive).OrderBy(p => p.Name).Select(p => p.Name));
        SyncFilterOptions(WhoFilterOptions, desired);
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

    // The active filter state, resolved once per pass. Previously each of the three multi-selects
    // was re-scanned into a fresh List for every card tested, so a board of N cards allocated 3N
    // lists on every keystroke in the Keyword box. HashSet also turns the membership test from a
    // linear scan into a lookup; it uses the default ordinal comparer, exactly like List.Contains
    // did, so which cards match is unchanged.
    private readonly record struct FilterCriteria(
        HashSet<string> Projects,
        HashSet<string> Priorities,
        HashSet<string> Whos,
        string Goal,
        string Flag,
        string Due,
        DateTime Today,
        DateTime? RangeFrom,
        DateTime? RangeTo,
        string? Keyword);

    private FilterCriteria BuildFilterCriteria() => new(
        ProjectFilterOptions.Where(o => o.IsSelected).Select(o => o.Name).ToHashSet(),
        PriorityFilterOptions.Where(o => o.IsSelected).Select(o => o.Name).ToHashSet(),
        WhoFilterOptions.Where(o => o.IsSelected).Select(o => o.Name).ToHashSet(),
        SelectedGoalFilter,
        SelectedFlagFilter,
        DueFilter,
        DateTime.Today,
        DueRangeFrom,
        DueRangeTo,
        string.IsNullOrWhiteSpace(KeywordFilter) ? null : KeywordFilter.Trim());

    public void ApplyFilters()
    {
        var criteria = BuildFilterCriteria();
        foreach (var column in Columns)
        {
            foreach (var card in column.Cards)
            {
                card.IsVisible = Matches(card, criteria);
            }
        }
    }

    private bool MatchesFilters(CardViewModel card) => Matches(card, BuildFilterCriteria());

    private static bool Matches(CardViewModel card, in FilterCriteria criteria)
    {
        if (criteria.Projects.Count > 0 && !criteria.Projects.Contains(card.ProjectName)) return false;

        if (criteria.Priorities.Count > 0 && !criteria.Priorities.Contains(card.Priority)) return false;

        if (criteria.Whos.Count > 0)
        {
            var whoKey = card.WhoId is null ? "Unassigned" : card.WhoName;
            if (!criteria.Whos.Contains(whoKey)) return false;
        }

        if (criteria.Goal == "Unassigned")
        {
            if (card.GoalId is not null) return false;
        }
        else if (criteria.Goal != "All" && card.GoalName != criteria.Goal) return false;

        // Copied to a local because an `in` parameter can't be captured by the lambda below.
        var flag = criteria.Flag;
        if (flag == "Unassigned")
        {
            if (card.Flags.Count > 0) return false;
        }
        else if (flag != "All" && card.Flags.All(f => f.Name != flag)) return false;

        if (criteria.Due != "All")
        {
            var today = criteria.Today;
            var matchesDue = criteria.Due switch
            {
                "Today" => card.DueDate is not null && card.DueDate.Value.Date <= today,
                "Tomorrow" => card.DueDate?.Date == today.AddDays(1),
                "Within a Week" => card.DueDate is not null && card.DueDate.Value.Date <= today.AddDays(7),
                "No Due Date" => card.DueDate is null,
                _ => true
            };
            if (!matchesDue) return false;
        }

        if (criteria.RangeFrom is not null || criteria.RangeTo is not null)
        {
            if (card.DueDate is null) return false;
            if (criteria.RangeFrom is not null && card.DueDate.Value.Date < criteria.RangeFrom.Value.Date) return false;
            if (criteria.RangeTo is not null && card.DueDate.Value.Date > criteria.RangeTo.Value.Date) return false;
        }

        if (criteria.Keyword is { } keyword)
        {
            var matchesKeyword = card.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || card.ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || card.WhoName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (card.Notes?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
            if (!matchesKeyword) return false;
        }

        return true;
    }

    // Sets IsSelected on whichever options match the given names (used to restore a saved
    // selection - a custom filter slot, or the remembered last-view state - onto a freshly
    // refreshed options list). A saved name that no longer exists as an option is silently
    // dropped, the same as an invalid single-select value used to fall back to "All".
    private static void ApplySelection(IEnumerable<FilterOptionViewModel> options, IEnumerable<string> selectedNames)
    {
        var names = selectedNames as ICollection<string> ?? selectedNames.ToList();
        foreach (var option in options) option.IsSelected = names.Contains(option.Name);
    }

    // Alt+T's one-key "just show me today". Every other filter is reset first, so the result is
    // always the whole board's due-today (and overdue) work rather than today's slice of whatever
    // narrowing happened to be applied already.
    public void ShowTodayOnly()
    {
        ClearFilters();
        DueFilter = "Today";
    }

    public void ClearFilters()
    {
        foreach (var option in ProjectFilterOptions) option.IsSelected = false;
        foreach (var option in PriorityFilterOptions) option.IsSelected = false;
        foreach (var option in WhoFilterOptions) option.IsSelected = false;

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
}
