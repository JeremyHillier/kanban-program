using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class DeletedTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly List<DeletedCardInfo> _allItems;
    private readonly ObservableCollection<DeletedCardInfo> _items;

    public DeletedTasksWindow(MainViewModel viewModel, List<DeletedCardInfo> deletedCards)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        _allItems = deletedCards;
        _items = new ObservableCollection<DeletedCardInfo>(deletedCards);
        DeletedList.ItemsSource = _items;
        UpdateEmptyState();
    }

    private void DateRange_Changed(object sender, SelectionChangedEventArgs e) => ApplyDateFilter();

    private void ClearDates_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.SelectedDate = null;
        ToDatePicker.SelectedDate = null;
        ApplyDateFilter();
    }

    private void ApplyDateFilter()
    {
        var from = FromDatePicker.SelectedDate;
        var to = ToDatePicker.SelectedDate;

        _items.Clear();
        foreach (var item in _allItems.Where(i => MatchesDateRange(i.DeletedAt, from, to)))
        {
            _items.Add(item);
        }

        UpdateEmptyState();
    }

    // Items whose deleted date can't be parsed (shouldn't normally happen) are never hidden by a
    // date filter - better to show something unclassifiable than to silently drop it from the list.
    private static bool MatchesDateRange(string timestamp, DateTime? from, DateTime? to)
    {
        if (from is null && to is null) return true;
        if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return true;

        if (from is not null && date.Date < from.Value.Date) return false;
        if (to is not null && date.Date > to.Value.Date) return false;
        return true;
    }

    private void Reactivate_Click(object sender, RoutedEventArgs e)
    {
        if (DeletedList.SelectedItem is not DeletedCardInfo selected)
        {
            MessageBox.Show(this, "Select a task to reactivate first.", "No Task Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _viewModel.ReactivateCard(selected.Id, selected.Title);
        _items.Remove(selected);
        _allItems.Remove(selected);
        UpdateEmptyState();
    }

    private void DeletedItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DeletedCardInfo item } element) return;

        var menu = new ContextMenu();
        var deleteItem = new MenuItem { Header = "Permanently Delete..." };
        deleteItem.Click += (_, _) => ConfirmAndPermanentlyDelete(item);
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = element;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ConfirmAndPermanentlyDelete(DeletedCardInfo item)
    {
        var result = MessageBox.Show(this,
            $"Permanently delete \"{item.Title}\"?\n\nThis cannot be undone. Any attachments still stored with it will be deleted too.",
            "Permanently Delete Task", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        // Deferred via BeginInvoke: fires from a ContextMenu opened on the very item's container
        // that's about to be removed - the same WPF deadlock documented on the board's quick-edit
        // popups if the mutation happens synchronously.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.PermanentlyDeleteCard(item.Id, item.Title, "Deleted");
            _items.Remove(item);
            _allItems.Remove(item);
            UpdateEmptyState();
        }), DispatcherPriority.Background);
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
