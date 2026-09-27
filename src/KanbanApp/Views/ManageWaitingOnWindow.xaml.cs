using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Add to, reword and prune the answers suggested for Waiting On. Each line shows how many tasks
// on the board say it now, since renaming rewords those tasks and deleting can clear them.
public partial class ManageWaitingOnWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageWaitingOnWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Reload();
    }

    private void Reload(string? select = null)
    {
        var entries = _viewModel.WaitingOnEntries;
        EntryList.ItemsSource = entries;
        EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EntryList.SelectedItem = select is null ? null : entries.FirstOrDefault(e => string.Equals(e.Text, select.Trim(), StringComparison.OrdinalIgnoreCase));
        if (EntryList.SelectedItem is not null) EntryList.ScrollIntoView(EntryList.SelectedItem);
    }

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenameButton.IsEnabled = DeleteButton.IsEnabled = EntryList.SelectedItem is MainViewModel.WaitingOnEntry;
    }

    private void EntryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) Delete_Click(sender, e);
        else if (e.Key == Key.F2) Rename_Click(sender, e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow("Add to Waiting On List", "Who or what might a task be waiting on?") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (!_viewModel.AddWaitingOnSuggestion(dialog.Value))
        {
            Dialogs.Tell(this, "Waiting On List", $"\"{dialog.Value}\" is already on the list.");
        }

        Reload(dialog.Value);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is not MainViewModel.WaitingOnEntry entry) return;

        var dialog = new PromptWindow("Rename", entry.TaskCount == 0 ? "New wording" : $"New wording (also changes {TaskWord(entry.TaskCount)})", entry.Text, "Save") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;

        _viewModel.RenameWaitingOnSuggestion(entry.Text, dialog.Value);
        Reload(dialog.Value);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is not MainViewModel.WaitingOnEntry entry) return;

        if (entry.TaskCount == 0)
        {
            _viewModel.DeleteWaitingOnSuggestion(entry.Text, clearFromTasks: false);
            Reload();
            return;
        }

        var one = entry.TaskCount == 1;
        var answer = Dialogs.Show(this, new DialogMessage("Delete from Waiting On List",
            $"{Capitalised(TaskWord(entry.TaskCount))} {(one ? "is" : "are")} still waiting on \"{entry.Text}\". Clear it from {(one ? "that task" : "those tasks")} too?\n\n" +
            $"• Clear From {(one ? "Task" : "Tasks")}: it leaves the list and the {(one ? "task" : "tasks")}. Undo brings it back on the {(one ? "task" : "tasks")}.\n" +
            $"• Leave {(one ? "the Task" : "the Tasks")}: the {(one ? "task keeps" : "tasks keep")} it, so it stays on this list for as long as a task says it.")
        {
            Tone = DialogTone.Question,
            Yes = one ? "Clear From Task" : "Clear From Tasks",
            No = one ? "Leave the Task" : "Leave the Tasks",
            Cancel = "Cancel",
        });
        if (answer == DialogChoice.Cancel) return;

        _viewModel.DeleteWaitingOnSuggestion(entry.Text, clearFromTasks: answer == DialogChoice.Yes);
        Reload();
    }

    private static string TaskWord(int count) => count == 1 ? "1 task" : $"{count} tasks";

    private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
