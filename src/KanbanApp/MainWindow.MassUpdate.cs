using System.IO;
using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// Mass Update and Copy to Another Task File for the selected cards (the selection menu's items).
public partial class MainWindow
{
    private void MassUpdate(List<CardViewModel> cards, MainViewModel viewModel)
    {
        var dialog = new MassUpdateWindow(viewModel, cards.Count) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var moved = viewModel.ApplyMassUpdate(cards, dialog.Changes);
            // As with dragging a group: no "add a note?" for each task, but a task set to force an edit still opens.
            if (dialog.Changes.Column is { } column)
            {
                foreach (var card in moved) MaybePromptCompletionNote(card, column, viewModel, askForNote: false);
                MaybePromptWaitingOn(moved, column, viewModel);
                MaybePromptNotifySender(moved, column, viewModel);
            }
            ShowStatusMessage($"{cards.Count} task{(cards.Count == 1 ? "" : "s")} updated");
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Mass Update", "Something went wrong, so the update was undone and no task was changed.", DialogTone.Error, ex.Message);
        }
    }

    // The other task files on this PC's list; the chosen one gets copies of the tasks.
    private void AddCopyToFileMenu(System.Windows.Controls.ContextMenu menu, List<CardViewModel> cards, MainViewModel viewModel)
    {
        var config = AppConfig.Load();
        var others = config.FilesByRecency.Where(f => !AppConfig.ArePathsEqual(f.Path, viewModel.CurrentDbPath) && File.Exists(f.Path)).ToList();
        var copy = AddSubmenu(menu, "Copy to Another Task File");
        copy.IsEnabled = others.Count > 0;
        foreach (var file in others)
        {
            AddMenuItem(copy, MenuText(file.DisplayName) + "...", () => CopyToFile(cards, viewModel, file));
        }
    }

    private void CopyToFile(List<CardViewModel> cards, MainViewModel viewModel, TaskFileEntry file)
    {
        if (!Dialogs.Confirm(this, DialogMessage.Ask("Copy to Another Task File",
                $"Copy {cards.Count} task{(cards.Count == 1 ? "" : "s")} to \"{file.DisplayName}\"?\n\nThe originals stay here. Projects, goals, people and flags are matched by name there, and added if missing. Attachments and links to other tasks are not copied.", "Copy") with { Detail = file.Path })) return;
        try
        {
            var count = TaskCopyService.Copy(cards.Select(c => (c, viewModel.ColumnOf(c) is { } column ? (column.IsCustom ? column.DisplayName : column.Name) : "To Do")), file.Path);
            ShowStatusMessage($"{count} task{(count == 1 ? "" : "s")} copied to {file.DisplayName}");
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Copy to Another Task File", "The tasks were not copied.", DialogTone.Error, ex.Message);
        }
    }
}
