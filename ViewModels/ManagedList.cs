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

    public void Add(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var model = _dbAdd(name.Trim());
        InsertSortedByName(_factory(model));
        _onRefreshFilterOptions();
    }

    public void Rename(TViewModel item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || item.Name == newName.Trim()) return;

        item.Name = newName.Trim();
        _dbRename(item.Id, item.Name);
        _items.Remove(item);
        InsertSortedByName(item);

        _onRenamedSyncCards(item);
        _onRefreshFilterOptions();
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
