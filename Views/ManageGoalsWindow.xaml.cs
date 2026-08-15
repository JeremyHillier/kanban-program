using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageGoalsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageGoalsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
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

        _viewModel.RenameGoal(goal, textBox.Text);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GoalViewModel goal }) return;

        if (_viewModel.ConfirmDelete)
        {
            var result = MessageBox.Show(this, $"Delete goal \"{goal.Name}\"? Tasks using it will show as having no goal.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        _viewModel.DeleteGoal(goal);
    }
}
