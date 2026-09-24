using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// What the user is told when a project, goal, flag or person name they asked for is already taken.
// Shared by the Manage screen (all four lists) and the task screen's "+" links so the wording stays the same.
internal static class ManagedListPrompts
{
    public static void ShowAddNotice<T>(Window owner, ManagedAddResult<T> result, string kind, string name)
    {
        if (result.Notice(kind, name) is not { } notice) return;
        MessageBox.Show(owner, notice, "Name Already Used", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // The box still shows what was typed; put the real name back, since nothing was renamed.
    public static void RenameRefused(Window owner, TextBox box, string kind, string name)
    {
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        MessageBox.Show(owner, $"There is already a {kind} called \"{name.Trim()}\", so the name was not changed.",
            "Name Already Used", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
