using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ArchivedTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ObservableCollection<ArchivedCardInfo> _items;

    public ArchivedTasksWindow(MainViewModel viewModel, List<ArchivedCardInfo> archivedCards)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        _items = new ObservableCollection<ArchivedCardInfo>(archivedCards);
        ArchivedList.ItemsSource = _items;
        EmptyStateText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Reactivate_Click(object sender, RoutedEventArgs e)
    {
        if (ArchivedList.SelectedItem is not ArchivedCardInfo selected)
        {
            MessageBox.Show(this, "Select a task to reactivate first.", "No Task Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Reactivate(selected);
    }

    private void ArchivedList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
        _items.Remove(selected);
        EmptyStateText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
