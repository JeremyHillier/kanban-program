using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageFlagsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageFlagsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
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

        // Deferred via BeginInvoke: RenameFlag re-sorts the list (Remove+Insert, not Move), which
        // tears down this very TextBox's row while its own LostFocus event is still dispatching —
        // the same WPF deadlock documented on the board's quick-edit popups.
        var newName = textBox.Text;
        Dispatcher.BeginInvoke(new Action(() => _viewModel.RenameFlag(flag, newName)), DispatcherPriority.Background);
    }

    private void Active_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: FlagViewModel flag } checkBox) return;

        _viewModel.SetFlagActive(flag, checkBox.IsChecked == true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FlagViewModel flag }) return;

        if (_viewModel.ConfirmDelete)
        {
            var count = _viewModel.CountTasksUsingFlag(flag);
            var impact = count == 0 ? "No tasks currently use it." : $"{count} task{(count == 1 ? "" : "s")} currently use it — they'll lose this flag.";
            var result = MessageBox.Show(this, $"Delete flag \"{flag.Name}\"?\n\n{impact}\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        // Deferred via BeginInvoke: same reason as the rename above — removing the row tears down
        // this button's own container mid-Click-dispatch.
        Dispatcher.BeginInvoke(new Action(() => _viewModel.DeleteFlag(flag)), DispatcherPriority.Background);
    }
}
