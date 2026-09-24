using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Archived tasks: filter by when they were archived, bring one back (button or double-click), or
// permanently delete it. The list logic is shared with Deleted Tasks in RecoverableTaskList.
public partial class ArchivedTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly RecoverableTaskList<ArchivedCardInfo> _list;

    public ArchivedTasksWindow(MainViewModel viewModel, List<ArchivedCardInfo> archivedCards)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        _list = new RecoverableTaskList<ArchivedCardInfo>(archivedCards, i => i.ArchivedAt);
        ArchivedList.ItemsSource = _list.Shown;
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
        if (ArchivedList.SelectedItem is not ArchivedCardInfo selected)
        {
            RecoverableTaskPrompts.NothingSelected(this);
            return;
        }

        Reactivate(selected);
    }

    private void ArchivedList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ArchivedList.SelectedItem is not ArchivedCardInfo selected) return;

        // Deferred via BeginInvoke: this double-click still bubbles up from the clicked ListBoxItem,
        // so removing it from the bound list synchronously here tears down that item's own container
        // mid-dispatch — the same WPF deadlock documented on the board's quick-edit popups.
        Dispatcher.BeginInvoke(new Action(() => Reactivate(selected)), DispatcherPriority.Background);
    }

    private void Reactivate(ArchivedCardInfo selected)
    {
        _viewModel.ReactivateCard(selected.Id, selected.Title);
        _list.Remove(selected);
        UpdateEmptyState();
    }

    private void ArchivedItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ArchivedCardInfo item } element) return;

        RecoverableTaskPrompts.ShowPermanentDeleteMenu(element, () => ConfirmAndPermanentlyDelete(item));
        e.Handled = true;
    }

    private void ConfirmAndPermanentlyDelete(ArchivedCardInfo item)
    {
        if (!RecoverableTaskPrompts.ConfirmPermanentDelete(this, item.Title)) return;

        // Deferred via BeginInvoke: same reasoning as Reactivate above - this fires from a
        // ContextMenu opened on the very item's container that's about to be removed.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.PermanentlyDeleteCard(item.Id, item.Title, "Archived");
            _list.Remove(item);
            UpdateEmptyState();
        }), DispatcherPriority.Background);
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = _list.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }
}
