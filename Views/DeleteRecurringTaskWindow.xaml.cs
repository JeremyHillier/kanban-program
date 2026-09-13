using System.Windows;

namespace KanbanApp.Views;

public partial class DeleteRecurringTaskWindow : Window
{
    // null until a choice button is clicked; Cancel/closing the window leaves it null.
    public bool? SpawnNext { get; private set; }

    public DeleteRecurringTaskWindow(string cardTitle)
    {
        InitializeComponent();
        MessageText.Text = $"\"{cardTitle}\" is a recurring task. What would you like to do?";
    }

    private void KeepSeries_Click(object sender, RoutedEventArgs e)
    {
        SpawnNext = true;
        DialogResult = true;
        Close();
    }

    private void EndSeries_Click(object sender, RoutedEventArgs e)
    {
        SpawnNext = false;
        DialogResult = true;
        Close();
    }
}
