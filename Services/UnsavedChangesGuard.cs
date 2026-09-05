using System.Windows;

namespace KanbanApp.Services;

// Shared "you'll lose what you typed" prompt for the three dialogs that hold data until an explicit
// Save/Add: the Add/Edit Task dialog, the imported-tasks review grid, and the small name prompt.
// Every other dialog either writes each change straight through as you make it, or is read-only, so
// closing one of those can't discard anything.
public static class UnsavedChangesGuard
{
    /// <summary>Asks whether to discard. True means close and lose the changes.</summary>
    public static bool ConfirmDiscard(Window owner) =>
        MessageBox.Show(owner,
            "You have unsaved changes.\n\nClose without saving?",
            "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            // Default to the safe answer, so a stray Enter on the prompt keeps the work.
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
