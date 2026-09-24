using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace KanbanApp.Views;

// The workings Archived Tasks and Deleted Tasks share: the full list, the part the From/To dates
// let through, and taking a task out once it's brought back or permanently deleted. The two
// windows keep their own layouts (they differ on purpose); only this logic is shared.
internal sealed class RecoverableTaskList<T>(List<T> items, Func<T, string> stamp) where T : class
{
    public ObservableCollection<T> Shown { get; } = new(items);
    public bool IsEmpty => Shown.Count == 0;

    public void Filter(DateTime? from, DateTime? to)
    {
        Shown.Clear();
        foreach (var item in items.Where(i => MatchesDateRange(stamp(i), from, to))) Shown.Add(item);
    }

    public void Remove(T item)
    {
        Shown.Remove(item);
        items.Remove(item);
    }

    // Items whose date can't be read (shouldn't normally happen) are never hidden by a date filter -
    // better to show something unclassifiable than to silently drop it from the list. Both ends of
    // the range count.
    internal static bool MatchesDateRange(string timestamp, DateTime? from, DateTime? to)
    {
        if (from is null && to is null) return true;
        if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return true;

        if (from is not null && date.Date < from.Value.Date) return false;
        if (to is not null && date.Date > to.Value.Date) return false;
        return true;
    }
}

// The right-click "Permanently Delete..." on a task in either window, and the question it asks.
internal static class RecoverableTaskPrompts
{
    public static void ShowPermanentDeleteMenu(FrameworkElement element, Action onChoose)
    {
        var menu = new ContextMenu();
        var deleteItem = new MenuItem { Header = "Permanently Delete..." };
        deleteItem.Click += (_, _) => onChoose();
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = element;
        menu.IsOpen = true;
    }

    public static bool ConfirmPermanentDelete(Window owner, string title) =>
        MessageBox.Show(owner,
            $"Permanently delete \"{title}\"?\n\nThis cannot be undone. Any attachments still stored with it will be deleted too.",
            "Permanently Delete Task", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public static void NothingSelected(Window owner) =>
        MessageBox.Show(owner, "Select a task to reactivate first.", "No Task Selected", MessageBoxButton.OK, MessageBoxImage.Information);
}
