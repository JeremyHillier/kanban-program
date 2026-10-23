using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// A goal's milestones: add, reword, move and delete them. Each shows on the Timeline, grouped by goal, as a
// vertical line on the goal's row.
public partial class GoalMilestonesWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly GoalViewModel _goal;

    public GoalMilestonesWindow(MainViewModel viewModel, GoalViewModel goal)
    {
        _viewModel = viewModel;
        _goal = goal;
        InitializeComponent();
        AccessKeys.Bind(this);
        Title = $"Milestones - {goal.Name}";
        IntroText.Text = $"Dated points on the way to \"{goal.Name}\". Each is drawn on the Timeline, grouped by goal.";
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        NewDatePicker.SelectedDate = DateTime.Today;
        ShowList();
        Loaded += (_, _) => NewDescriptionTextBox.Focus();
    }

    private void ShowList() => ItemsList.ItemsSource = _viewModel.MilestonesOf(_goal);

    // The mouse wheel moves the date, like every other date box in the app.
    private void Picker_Loaded(object sender, RoutedEventArgs e) => CalendarWheelSupport.Attach((DatePicker)sender);

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (NewDatePicker.SelectedDate is not { } date)
        {
            Dialogs.Tell(this, "Choose a Date", "Pick the day this milestone is due.");
            return;
        }

        if (!_viewModel.AddMilestone(_goal, NewDescriptionTextBox.Text, date))
        {
            Dialogs.Tell(this, "Describe the Milestone", "Type what the milestone is, for example \"Contract signed\".");
            NewDescriptionTextBox.Focus();
            return;
        }

        NewDescriptionTextBox.Clear();
        NewDescriptionTextBox.Focus();
        ShowList();
    }

    private void Description_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: GoalMilestone milestone } box) return;

        // A blank description is not kept: the row goes back to what it said.
        if (string.IsNullOrWhiteSpace(box.Text)) box.Text = milestone.Description;
        else _viewModel.ChangeMilestone(milestone, box.Text, milestone.Date);
    }

    private void Date_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DatePicker { DataContext: GoalMilestone milestone } picker || picker.SelectedDate is not { } date || milestone.Date == date.Date) return;

        _viewModel.ChangeMilestone(milestone, milestone.Description, date);

        // The list is in date order, so it is redrawn - after this event, since it rebuilds this very row.
        Dispatcher.BeginInvoke(new Action(ShowList), DispatcherPriority.Background);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GoalMilestone milestone }) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.DeleteMilestone(milestone);
            ShowList();
        }), DispatcherPriority.Background);
    }
}
