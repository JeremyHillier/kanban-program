using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Deleted tasks: filter by when they were deleted, bring one back, or permanently delete it. The
// list logic is shared with Archived Tasks in RecoverableTaskList.
public partial class DeletedTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly RecoverableTaskList<DeletedCardInfo> _list;

    public DeletedTasksWindow(MainViewModel viewModel, List<DeletedCardInfo> deletedCards)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        _list = new RecoverableTaskList<DeletedCardInfo>(deletedCards, i => i.DeletedAt);
        DeletedList.ItemsSource = _list.Shown;
        UpdateEmptyState();
    }

    private void DateRange_Changed(object sender, SelectionChangedEventArgs e) => ApplyDateFilter();

    private void DatePicker_Loaded(object sender, RoutedEventArgs e) => CalendarWheelSupport.Attach((DatePicker)sender);

    private void ClearDates_Click(object sender, RoutedEventArgs e)
    {
        FromDatePicker.SelectedDate = null;
        ToDatePicker.SelectedDate = null;
        ApplyDateFilter();
    }

    private void ApplyDateFilter()
    {
        _list.Filter(FromDatePicker.SelectedDate, ToDatePicker.SelectedDate);
        UpdateEmptyState();
    }

    private void Reactivate_Click(object sender, RoutedEventArgs e)
    {
        if (DeletedList.SelectedItem is not DeletedCardInfo selected)
        {
            RecoverableTaskPrompts.NothingSelected(this);
            return;
        }

        _viewModel.ReactivateCard(selected.Id, selected.Title);
        _list.Remove(selected);
        UpdateEmptyState();
    }

    private void DeletedItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DeletedCardInfo item } element) return;

        RecoverableTaskPrompts.ShowPermanentDeleteMenu(element, () => ConfirmAndPermanentlyDelete(item));
        e.Handled = true;
    }

    private void ConfirmAndPermanentlyDelete(DeletedCardInfo item)
    {
        if (!RecoverableTaskPrompts.ConfirmPermanentDelete(this, item.Title)) return;

        // Deferred via BeginInvoke: fires from a ContextMenu opened on the very item's container
        // that's about to be removed - the same WPF deadlock documented on the board's quick-edit
        // popups if the mutation happens synchronously.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.PermanentlyDeleteCard(item.Id, item.Title, "Deleted");
            _list.Remove(item);
            UpdateEmptyState();
        }), DispatcherPriority.Background);
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = _list.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }
}
