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

    // Deleting a selection that includes recurring tasks: the same two choices, worded for a group.
    // The answer applies to every recurring task in it; the other tasks are simply deleted.
    public DeleteRecurringTaskWindow(int taskCount, int recurringCount)
    {
        InitializeComponent();
        Title = "Delete Tasks";
        var repeat = recurringCount == 1 ? "1 of them is a recurring task" : $"{recurringCount} of them are recurring tasks";
        MessageText.Text = $"Delete {taskCount} tasks? This cannot be undone.\n\n{repeat}. What would you like to do with {(recurringCount == 1 ? "it" : "those")}?";
        KeepSeriesButton.Content = "Delete All, Keep Recurring Series Going";
        KeepSeriesNote.Text = "Deletes every selected task, and creates the next occurrence of each recurring one right away.";
        EndSeriesButton.Content = "Delete All, End Recurring Series";
        EndSeriesNote.Text = "Deletes every selected task and stops each recurring series - no further occurrences will be created.";
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
