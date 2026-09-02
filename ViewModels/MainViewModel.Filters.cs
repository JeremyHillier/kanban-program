using System.Collections.ObjectModel;

namespace KanbanApp.ViewModels;

// The sidebar's filter dropdowns/keyword box/due-date range: option lists, selected values, and
// the MatchesFilters predicate that drives each card's IsVisible.
public partial class MainViewModel
{
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
}
