using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageGoalsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageGoalsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NewGoalTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        _viewModel.AddGoal(name);
        NewGoalTextBox.Clear();
        NewGoalTextBox.Focus();
    }

    private void GoalName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: GoalViewModel goal } textBox) return;

        // Deferred via BeginInvoke: RenameGoal re-sorts the list (Remove+Insert, not Move), which
        // tears down this very TextBox's row while its own LostFocus event is still dispatching —
        // the same WPF deadlock documented on the board's quick-edit popups.
        var newName = textBox.Text;
        Dispatcher.BeginInvoke(new Action(() => _viewModel.RenameGoal(goal, newName)), DispatcherPriority.Background);
    }

    private void Active_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: GoalViewModel goal } checkBox) return;

        _viewModel.SetGoalActive(goal, checkBox.IsChecked == true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GoalViewModel goal }) return;

        if (_viewModel.ConfirmDelete)
        {
            var count = _viewModel.CountTasksUsingGoal(goal);
            var impact = count == 0 ? "No tasks currently use it." : $"{count} task{(count == 1 ? "" : "s")} currently use it — they'll show as having no goal.";
            var result = MessageBox.Show(this, $"Delete goal \"{goal.Name}\"?\n\n{impact}\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        // Deferred via BeginInvoke: same reason as the rename above — removing the row tears down
        // this button's own container mid-Click-dispatch.
        Dispatcher.BeginInvoke(new Action(() => _viewModel.DeleteGoal(goal)), DispatcherPriority.Background);
    }
}
