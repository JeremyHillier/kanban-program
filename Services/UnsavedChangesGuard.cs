using System.Windows;

namespace KanbanApp.Services;

// Shared "you'll lose what you typed" prompt for the three dialogs that hold data until an explicit
// Save/Add: the Add/Edit Task dialog, the imported-tasks review grid, and the small name prompt.
// Every other dialog either writes each change straight through as you make it, or is read-only, so
// closing one of those can't discard anything.
public static class UnsavedChangesGuard
{
    /// <summary>Asks whether to discard. True means close and lose the changes.</summary>
    // Enter chooses Keep Editing, the safe answer, so a stray Enter on the prompt keeps the work.
    public static bool ConfirmDiscard(Window owner) =>
        Dialogs.Confirm(owner, DialogMessage.AskDanger("Unsaved Changes",
            "Close without saving?\n\nThe changes made here will be lost.", "Close Without Saving", "Keep Editing"));
}
