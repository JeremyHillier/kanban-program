using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AddTaskWindow : Window
{
    public string TaskDetails { get; private set; } = string.Empty;
    public ColumnViewModel? SelectedColumn { get; private set; }

    public AddTaskWindow(IEnumerable<ColumnViewModel> columns)
    {
        InitializeComponent();
        CategoryComboBox.ItemsSource = columns;
        DetailsTextBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var details = DetailsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(details) || CategoryComboBox.SelectedItem is not ColumnViewModel column)
        {
            return;
        }

        TaskDetails = details;
        SelectedColumn = column;
        DialogResult = true;
        Close();
    }
}
