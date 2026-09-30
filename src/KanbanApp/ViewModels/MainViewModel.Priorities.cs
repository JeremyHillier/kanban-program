using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// The task file's own priority list (see PriorityList): adding to it, renaming, reordering,
// recolouring and deleting, from the Manage Priorities screen. A task stores its priority as the
// name, so a rename or a delete rewords every task that has it - on the board, archived and
// deleted - along with task templates, custom filters and saved report views.
//
// Like the other lists (projects, people...) these changes are outside Undo. A rename or a delete
// also empties the Undo history: its saved copies of tasks still carry the old name, and undoing
// one would bring that name back.
public partial class MainViewModel
{
    public PriorityList Priorities { get; }

    // One line on the Manage Priorities screen. TaskCount includes archived tasks.
    public sealed record PriorityEntry(string Name, string Color, bool IsDefault, int TaskCount)
    {
        public string Detail =>
            (IsDefault ? "default for new tasks  ·  " : "") + (TaskCount == 1 ? "1 task" : $"{TaskCount} tasks");
    }

    public List<PriorityEntry> PriorityEntries
    {
        get
        {
            var counts = _db.CountTasksByPriority();
            return Priorities.Levels
                .Select(l => new PriorityEntry(l.Name, l.Color, l.Name == Priorities.Default, counts.GetValueOrDefault(l.Name)))
                .ToList();
        }
    }

    // False when the name is empty or already on the list.
    public bool AddPriority(string name)
    {
        if (!Priorities.Add(name)) return false;
        PrioritiesChanged();
        return true;
    }

    // False when the new name is empty or already belongs to another priority.
    public bool RenamePriority(string name, string newName)
    {
        var old = Priorities.Find(name);
        if (old is null || !Priorities.Rename(old, newName)) return false;

        ReplacePriorityEverywhere(old, Priorities.Find(newName)!, keepInFilters: true);
        PrioritiesChanged();
        return true;
    }

    // Takes a priority off the list; its tasks become the default priority. False for the default
    // itself, or the only one left.
    public bool DeletePriority(string name)
    {
        var old = Priorities.Find(name);
        if (old is null || !Priorities.Remove(old)) return false;

        ReplacePriorityEverywhere(old, Priorities.Default, keepInFilters: false);
        PrioritiesChanged();
        return true;
    }

    // places < 0 is up the list (a higher priority).
    public bool MovePriority(string name, int places)
    {
        if (!Priorities.Move(name, places)) return false;
        PrioritiesChanged();
        return true;
    }

    public bool SetPriorityColor(string name, string color)
    {
        if (!Priorities.SetColor(name, color)) return false;
        PrioritiesChanged();
        return true;
    }

    public bool SetDefaultPriority(string name)
    {
        if (!Priorities.SetDefault(name)) return false;
        PrioritiesChanged();
        return true;
    }

    private static bool SamePriority(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void ReplacePriorityEverywhere(string old, string replacement, bool keepInFilters)
    {
        _db.ChangeTaskPriority(old, replacement);
        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => SamePriority(c.Priority, old)))
        {
            card.Priority = replacement;
        }

        foreach (var template in TaskTemplates.Where(t => SamePriority(t.Priority, old)).ToList())
        {
            template.Priority = replacement;
            _db.SaveTaskTemplate(template);
        }

        // A filter or report that named the old priority follows a rename; after a delete it just
        // stops naming it (it must not start matching every task of the default priority instead).
        List<string> InFilter(List<string> names) => keepInFilters
            ? names.Select(n => SamePriority(n, old) ? replacement : n).Distinct().ToList()
            : names.Where(n => !SamePriority(n, old)).ToList();

        for (var slot = 0; slot < CustomFilters.Count; slot++)
        {
            if (!CustomFilters[slot].Priority.Any(n => SamePriority(n, old))) continue;
            CustomFilters[slot].Priority = InFilter(CustomFilters[slot].Priority);
            SaveCustomFilter(slot);
            CustomFilters[slot] = CustomFilters[slot]; // same nudge RenameCustomFilter gives the list
        }

        var viewsChanged = false;
        foreach (var view in SavedReportViews.Where(v => v.Priority.Any(n => SamePriority(n, old))))
        {
            view.Priority = InFilter(view.Priority);
            viewsChanged = true;
        }
        if (viewsChanged) PersistSavedReportViews();

        // The board's own filter: a renamed priority stays selected under its new name.
        var option = PriorityFilterOptions.FirstOrDefault(o => SamePriority(o.Name, old));
        if (option is not null)
        {
            var index = PriorityFilterOptions.IndexOf(option);
            PriorityFilterOptions.RemoveAt(index);
            if (keepInFilters) PriorityFilterOptions.Insert(index, new FilterOptionViewModel(replacement) { IsSelected = option.IsSelected });
        }

        ClearUndoHistory();
    }

    // Saves the list and brings everything that shows or uses it up to date.
    private void PrioritiesChanged()
    {
        _db.SetSetting(PriorityList.SettingKey, Priorities.ToJson());
        // From here on a task can have a priority that older copies of the app don't offer.
        if (!Priorities.IsStandard) _db.RaiseFileFormat(4);
        RefreshPriorityFilterOptions();
        foreach (var card in Columns.SelectMany(c => c.Cards)) card.RefreshPriority();
        ApplyFilters();
        ApplySort();
        OnPropertyChanged(nameof(Priorities));
    }

    private void RefreshPriorityFilterOptions() => SyncFilterOptions(PriorityFilterOptions, Priorities.Names.ToList());
}
