using System.Collections.ObjectModel;
using System.Windows;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class DeletedTasksWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ObservableCollection<DeletedCardInfo> _items;

    public DeletedTasksWindow(MainViewModel viewModel, List<DeletedCardInfo> deletedCards)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        _items = new ObservableCollection<DeletedCardInfo>(deletedCards);
        DeletedList.ItemsSource = _items;
        EmptyStateText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        EmptyStateText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
