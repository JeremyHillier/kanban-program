using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp;

// A linked task opened from a card's right-click Related Tasks. If the board's filters are hiding it, the
// user is asked whether to clear them so it can be seen where it sits; either way it opens for editing.
public partial class MainWindow
{
    internal void OpenRelatedTask(int id, MainViewModel viewModel)
    {
        var card = viewModel.FindCardById(id);
        if (card is null)
        {
            Dialogs.Tell(this, "Task Not on the Board", "That task is archived or deleted.\n\nOpen Archived Tasks or Deleted Tasks to bring it back.", DialogTone.Info);
            return;
        }

        if (!card.IsVisible && Dialogs.Confirm(this, new DialogMessage("Task Hidden by a Filter",
                $"\"{card.Title}\" is hidden by the board's filters.\n\nClear the filters so it shows on the board?")
            { Tone = DialogTone.Question, Yes = "Clear Filters", No = "Leave Them" }))
        {
            viewModel.ClearFilters();
        }

        viewModel.ClearCardSelection();
        if (card.IsVisible) card.IsSelected = true;
        EditCard(card, viewModel);
    }
}
