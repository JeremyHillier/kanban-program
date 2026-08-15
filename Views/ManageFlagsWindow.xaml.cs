using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageFlagsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageFlagsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NewFlagTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        _viewModel.AddFlag(name);
        NewFlagTextBox.Clear();
        NewFlagTextBox.Focus();
    }

    private void FlagName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: FlagViewModel flag } textBox) return;

        _viewModel.RenameFlag(flag, textBox.Text);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FlagViewModel flag }) return;

        _viewModel.DeleteFlag(flag);
    }
}
