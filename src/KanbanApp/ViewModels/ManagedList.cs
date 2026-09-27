using System.Collections.ObjectModel;

namespace KanbanApp.ViewModels;

/// <summary>
/// Shared Add/Rename/Delete/SetActive/CountUsage logic for the four managed lists
/// (Project/Person/Goal/Flag), which were previously near-identical copy-pasted blocks
/// in MainViewModel. The per-entity differences (DB calls, and how a rename/delete needs
/// to sync already-loaded cards) are supplied via constructor delegates.
/// </summary>
public class ManagedList<TModel, TViewModel> where TViewModel : ObservableObject, IManagedItem
{
    private readonly ObservableCollection<TViewModel> _items;
    private readonly Func<string, TModel> _dbAdd;
    private readonly Action<int, string> _dbRename;
    private readonly Action<int> _dbDelete;
    private readonly Action<int, bool> _dbSetActive;
    private readonly Func<TModel, TViewModel> _factory;
    private readonly Action _onRefreshFilterOptions;
    private readonly Action _onActiveChanged;
    private readonly Action<TViewModel> _onRenamedSyncCards;
    private readonly Action<TViewModel> _onDeletedSyncCards;
    private readonly Func<TViewModel, int> _countUsage;

    public ManagedList(ObservableCollection<TViewModel> items, Func<string, TModel> dbAdd, Action<int, string> dbRename,
        Action<int> dbDelete, Action<int, bool> dbSetActive, Func<TModel, TViewModel> factory,
        Action onRefreshFilterOptions, Action onActiveChanged,
        Action<TViewModel> onRenamedSyncCards, Action<TViewModel> onDeletedSyncCards, Func<TViewModel, int> countUsage)
    {
        _items = items;
        _dbAdd = dbAdd;
        _dbRename = dbRename;
        _dbDelete = dbDelete;
        _dbSetActive = dbSetActive;
        _factory = factory;
        _onRefreshFilterOptions = onRefreshFilterOptions;
        _onActiveChanged = onActiveChanged;
        _onRenamedSyncCards = onRenamedSyncCards;
        _onDeletedSyncCards = onDeletedSyncCards;
        _countUsage = countUsage;
    }

    // Names are unique whatever their capitals. Asking for one that is already there adds nothing
    // and hands back the existing entry, switched back on if it had been retired - the user asked
    // for it by name, so they want to be able to pick it.
    public ManagedAddResult<TViewModel> Add(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return new(null, ManagedAddOutcome.Nothing);

        var existing = Find(name);
        if (existing is not null)
        {
            if (existing.IsActive) return new(existing, ManagedAddOutcome.AlreadyThere);

            SetActive(existing, true);
            return new(existing, ManagedAddOutcome.SwitchedBackOn);
        }

        var item = _factory(_dbAdd(name.Trim()));
        InsertSortedByName(item);
        _onRefreshFilterOptions();
        return new(item, ManagedAddOutcome.Added);
    }

    public TViewModel? Find(string name) =>
        _items.FirstOrDefault(i => string.Equals(i.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    // False when another entry already has that name: the rename is refused and nothing changes.
    public bool Rename(TViewModel item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || item.Name == newName.Trim()) return true;

        var other = Find(newName);
        if (other is not null && !ReferenceEquals(other, item)) return false;

        item.Name = newName.Trim();
        _dbRename(item.Id, item.Name);
        _items.Remove(item);
        InsertSortedByName(item);

        _onRenamedSyncCards(item);
        _onRefreshFilterOptions();
        return true;
    }

    public void Delete(TViewModel item)
    {
        _dbDelete(item.Id);
        _items.Remove(item);
        _onDeletedSyncCards(item);
        _onRefreshFilterOptions();
    }

    public void SetActive(TViewModel item, bool isActive)
    {
        if (item.IsActive == isActive) return;

        item.IsActive = isActive;
        _dbSetActive(item.Id, isActive);
        _onRefreshFilterOptions();
        _onActiveChanged();
    }

    public int CountUsage(TViewModel item) => _countUsage(item);

    private void InsertSortedByName(TViewModel item)
    {
        var index = 0;
        while (index < _items.Count && string.Compare(_items[index].Name, item.Name, StringComparison.OrdinalIgnoreCase) < 0)
        {
            index++;
        }
        _items.Insert(index, item);
    }
}

public enum ManagedAddOutcome { Nothing, Added, AlreadyThere, SwitchedBackOn }

public readonly record struct ManagedAddResult<TViewModel>(TViewModel? Item, ManagedAddOutcome Outcome)
{
    // What to tell the user when nothing new was added, or null when there is nothing to say.
    public string? Notice(string kind, string name) => Outcome switch
    {
        ManagedAddOutcome.AlreadyThere => $"There is already a {kind} called \"{name.Trim()}\", so a second one was not added.",
        ManagedAddOutcome.SwitchedBackOn => $"There is already a {kind} called \"{name.Trim()}\". It had been made inactive, so it has been made active again instead of adding a second one.",
        _ => null
    };
}
