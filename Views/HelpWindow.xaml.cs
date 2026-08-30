using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class HelpWindow : Window
{
    private readonly MainViewModel _viewModel;

    public HelpWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }
}
