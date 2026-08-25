using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageWhoWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageWhoWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NewPersonTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        _viewModel.AddPerson(name);
        NewPersonTextBox.Clear();
        NewPersonTextBox.Focus();
    }

    private void PersonName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: PersonViewModel person } textBox) return;

        // Deferred via BeginInvoke: RenamePerson re-sorts the list (Remove+Insert, not Move), which
        // tears down this very TextBox's row while its own LostFocus event is still dispatching —
        // the same WPF deadlock documented on the board's quick-edit popups.
        var newName = textBox.Text;
        Dispatcher.BeginInvoke(new Action(() => _viewModel.RenamePerson(person, newName)), DispatcherPriority.Background);
    }

    private void PersonEmail_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: PersonViewModel person } textBox) return;

        // Doesn't re-sort the list (email isn't a sort key), so unlike PersonName_LostFocus this
        // is safe to apply synchronously - no risk of tearing down this TextBox's own row mid-dispatch.
        _viewModel.SetPersonEmail(person, textBox.Text);
    }

    private void Active_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: PersonViewModel person } checkBox) return;

        _viewModel.SetPersonActive(person, checkBox.IsChecked == true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PersonViewModel person }) return;

        if (_viewModel.ConfirmDelete)
        {
            var count = _viewModel.CountTasksUsingPerson(person);
            var impact = count == 0 ? "No tasks currently use it." : $"{count} task{(count == 1 ? "" : "s")} currently use it — they'll show as unassigned.";
            var result = MessageBox.Show(this, $"Delete \"{person.Name}\"?\n\n{impact}\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        // Deferred via BeginInvoke: same reason as the rename above — removing the row tears down
        // this button's own container mid-Click-dispatch.
        Dispatcher.BeginInvoke(new Action(() => _viewModel.DeletePerson(person)), DispatcherPriority.Background);
    }
}
