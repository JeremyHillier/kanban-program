using System.Windows;
using System.Windows.Controls;
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

        _viewModel.RenamePerson(person, textBox.Text);
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
            var result = MessageBox.Show(this, $"Delete \"{person.Name}\"? {impact} This cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (result != MessageBoxResult.Yes) return;
        }

        _viewModel.DeletePerson(person);
    }
}
