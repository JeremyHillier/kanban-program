using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Manage Projects, Goals, Flags and Who - one screen, told which list by a ManagedListKind.
public partial class ManageListWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ManagedListKind _kind;

    // Read by the row template, so they are set before InitializeComponent builds it.
    public string ActiveToolTip { get; }
    public Visibility EmailVisibility { get; }
    public Visibility TargetVisibility { get; }

    internal ManageListWindow(MainViewModel viewModel, ManagedListKind kind)
    {
        _viewModel = viewModel;
        _kind = kind;
        ActiveToolTip = $"When off, this {kind.Noun} is hidden from filters and from selection on new/edited tasks. Tasks already using it are unaffected.";
        EmailVisibility = kind.HasEmail ? Visibility.Visible : Visibility.Collapsed;
        TargetVisibility = kind.HasTarget ? Visibility.Visible : Visibility.Collapsed;

        InitializeComponent();
        Title = kind.Title;
        if (kind.HasTarget) Width = 900; // a goal or project is one row: name, Active, start and target dates, Milestones, Delete
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        DataContext = viewModel;
        ItemsList.ItemsSource = kind.Items;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NewNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        _kind.Add(this, name);
        NewNameTextBox.Clear();
        NewNameTextBox.Focus();
    }

    private void Name_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: IManagedItem item } textBox) return;

        // Deferred via BeginInvoke: a rename re-sorts the list (Remove+Insert, not Move), which
        // tears down this very TextBox's row while its own LostFocus event is still dispatching —
        // the same WPF deadlock documented on the board's quick-edit popups.
        var newName = textBox.Text;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_kind.Rename(item, newName)) ManagedListPrompts.RenameRefused(this, textBox, _kind.Noun, newName);
        }), DispatcherPriority.Background);
    }

    private void Email_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: IManagedItem item } textBox) return;

        // Doesn't re-sort the list (email isn't a sort key), so unlike Name_LostFocus this
        // is safe to apply synchronously - no risk of tearing down this TextBox's own row mid-dispatch.
        _kind.SetEmail(item, textBox.Text);
    }

    // A goal's target date. The picker also raises this when the row is first drawn, so only a real change is saved.
    private void Target_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DatePicker { DataContext: IPlannedItem goal } picker || goal.TargetDate == picker.SelectedDate?.Date) return;

        _kind.SetTarget(goal, picker.SelectedDate);
    }

    // The mouse wheel moves the date, like every other date box in the app.
    private void TargetPicker_Loaded(object sender, RoutedEventArgs e) => KanbanApp.Services.CalendarWheelSupport.Attach((DatePicker)sender);

    private void Milestones_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: IPlannedItem goal }) new MilestonesWindow(_viewModel, goal) { Owner = this }.ShowDialog();
    }

    private void Start_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DatePicker { DataContext: IPlannedItem goal } picker || goal.StartDate == picker.SelectedDate?.Date) return;

        _kind.SetStart(goal, picker.SelectedDate);
    }

    private void ClearStart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: IPlannedItem goal }) _kind.SetStart(goal, null);
    }

    private void ClearTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: IPlannedItem goal }) _kind.SetTarget(goal, null);
    }

    private void Active_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: IManagedItem item } checkBox) return;

        _kind.SetActive(item, checkBox.IsChecked == true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: IManagedItem item }) return;

        if (_viewModel.ConfirmDelete)
        {
            var count = _kind.CountUsage(item);
            var impact = count == 0 ? "No tasks use it." : $"{count} task{(count == 1 ? " uses" : "s use")} it. If it is deleted, {_kind.IfDeleted}";
            // Undo does not cover the lists, so Enter keeps it: a quick Enter never deletes.
            if (!Dialogs.Confirm(this, DialogMessage.AskDanger("Delete",
                    $"{_kind.DeleteQuestion(item.Name)}\n\n{impact}\n\nThis cannot be undone.", "Delete")))
                return;
        }

        // Deferred via BeginInvoke: same reason as the rename above — removing the row tears down
        // this button's own container mid-Click-dispatch.
        Dispatcher.BeginInvoke(new Action(() => _kind.Delete(item)), DispatcherPriority.Background);
    }
}
