using System.Collections.ObjectModel;
using System.Text.Json;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// The ten Alt+0..Alt+9 filter slots: capturing the board's current filters into one, applying one
// back, and persisting them as JSON in the settings table (one key per slot).
public partial class MainViewModel
{
    public const int CustomFilterSlotCount = 10;

    // Always exactly CustomFilterSlotCount entries, indexed by slot number, so slot N is always
    // CustomFilters[N] whether or not it's been assigned yet.
    public ObservableCollection<CustomFilter> CustomFilters { get; } = [];

    private void LoadCustomFilters()
    {
        CustomFilters.Clear();
        for (var slot = 0; slot < CustomFilterSlotCount; slot++)
        {
            CustomFilters.Add(ReadCustomFilter(slot));
        }
    }

    private CustomFilter ReadCustomFilter(int slot)
    {
        var json = _db.GetSetting(CustomFilterKey(slot));
        if (string.IsNullOrWhiteSpace(json)) return new CustomFilter();

        try
        {
            return JsonSerializer.Deserialize<CustomFilter>(json) ?? new CustomFilter();
        }
        catch
        {
            // A corrupted slot shouldn't take the app down or block the other nine - treat it as unset.
            return new CustomFilter();
        }
    }

    private static string CustomFilterKey(int slot) => $"CustomFilter{slot}";

    private void SaveCustomFilter(int slot)
    {
        _db.SetSetting(CustomFilterKey(slot), JsonSerializer.Serialize(CustomFilters[slot]));
    }

    private static bool IsValidSlot(int slot) => slot >= 0 && slot < CustomFilterSlotCount;

    // Snapshots whatever the board is filtered by right now into the slot.
    public void CaptureCustomFilter(int slot, string name)
    {
        if (!IsValidSlot(slot)) return;

        CustomFilters[slot] = new CustomFilter
        {
            Name = name.Trim(),
            Project = ProjectFilterOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
            Priority = PriorityFilterOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
            Who = WhoFilterOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
            Goal = SelectedGoalFilter,
            Flag = SelectedFlagFilter,
            Due = DueFilter,
            DueFrom = DueRangeFrom?.ToString("yyyy-MM-dd"),
            DueTo = DueRangeTo?.ToString("yyyy-MM-dd"),
            Keyword = KeywordFilter
        };

        SaveCustomFilter(slot);
    }

    public void RenameCustomFilter(int slot, string name)
    {
        if (!IsValidSlot(slot) || string.IsNullOrWhiteSpace(name)) return;

        CustomFilters[slot].Name = name.Trim();
        SaveCustomFilter(slot);

        // The item instance is unchanged, so the list needs a nudge to re-read it.
        CustomFilters[slot] = CustomFilters[slot];
    }

    public void ClearCustomFilter(int slot)
    {
        if (!IsValidSlot(slot)) return;

        CustomFilters[slot] = new CustomFilter();
        _db.SetSetting(CustomFilterKey(slot), string.Empty);
    }

    // Applies slot N to the board. Returns false (leaving the board untouched) when the slot has
    // never been assigned, so an unused Alt+N does nothing rather than silently clearing filters.
    public bool ApplyCustomFilter(int slot)
    {
        if (!IsValidSlot(slot)) return false;

        var filter = CustomFilters[slot];
        if (!filter.IsDefined) return false;

        // Project/Priority/Who selection is set directly on the (already-live) FilterOptionViewModel
        // instances, which notify their own bound ListBoxItems. The rest of the backing fields are
        // set directly and the change notifications raised by hand, so the whole slot lands as one
        // atomic change with a single ApplyFilters pass at the end - going through the public setters
        // would re-filter the board repeatedly and, worse, let the DueFilter and DueRange setters
        // clear each other on the way through.
        ApplySelection(ProjectFilterOptions, filter.Project);
        ApplySelection(PriorityFilterOptions, filter.Priority);
        ApplySelection(WhoFilterOptions, filter.Who);

        _selectedGoalFilter = filter.Goal;
        _selectedFlagFilter = filter.Flag;
        _dueFilter = filter.Due;
        _dueRangeFrom = ParseDate(filter.DueFrom);
        _dueRangeTo = ParseDate(filter.DueTo);
        _keywordFilter = filter.Keyword;

        OnPropertyChanged(nameof(SelectedGoalFilter));
        OnPropertyChanged(nameof(SelectedFlagFilter));
        OnPropertyChanged(nameof(DueFilter));
        OnPropertyChanged(nameof(DueRangeFrom));
        OnPropertyChanged(nameof(DueRangeTo));
        OnPropertyChanged(nameof(KeywordFilter));

        ApplyFilters();
        return true;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, out var parsed) ? parsed : null;
}
