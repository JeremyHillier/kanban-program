using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The board's keyboard shortcuts.
public partial class MainWindow
{
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handled at the Window level (tunneling PreviewKeyDown, fires before any focused control's
        // own Escape handling) so it's a single, reliable "reset the view" regardless of which
        // filter control happens to have focus — a focused ComboBox's own "just close the dropdown"
        // Escape behavior otherwise leaves other filters untouched, which read as ESC only clearing
        // some of them.
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && DataContext is MainViewModel clearViewModel)
        {
            // Two steps: a multi-card selection is cleared first, then the next Esc clears filters.
            if (clearViewModel.SelectedCardCount > 0) clearViewModel.ClearCardSelection();
            else clearViewModel.ClearFilters();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
        {
            QuickReport_Click(sender, e);
            e.Handled = true;
            return;
        }

        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control:
                switch (e.Key)
                {
                    case Key.Q:
                        Close();
                        e.Handled = true;
                        break;
                    case Key.P:
                        ReportBuilder_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.Z:
                        // A text box keeps its own Ctrl+Z for typing.
                        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) break;
                        UndoLastAction();
                        e.Handled = true;
                        break;
                    case Key.N:
                        AddTask_Click(sender, e);
                        e.Handled = true;
                        break;
                }
                break;

            case ModifierKeys.Alt:
                switch (e.Key == Key.System ? e.SystemKey : e.Key)
                {
                    case Key.A:
                        ArchiveDone_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.P:
                        ManageProjects_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.G:
                        ManageGoals_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.F:
                        ManageFlags_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.W:
                        ManageWho_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.R:
                        Reminders_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.S:
                        Settings_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.H:
                        Help_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.L:
                        Timeline_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.M:
                        if (DataContext is MainViewModel templatesViewModel) ManageTemplates(templatesViewModel);
                        e.Handled = true;
                        break;
                    case Key.B:
                        (DataContext as MainViewModel)?.ToggleSidebar();
                        e.Handled = true;
                        break;
                    case Key.T:
                        if (DataContext is MainViewModel todayViewModel) todayViewModel.ShowTodayOnly();
                        e.Handled = true;
                        break;
                    default:
                        ApplyCustomFilterShortcut(e);
                        break;
                }
                break;
        }
    }

    // Alt+0 - Alt+9 apply that slot's saved filter. Both the number row (D0-D9) and the numeric
    // keypad (NumPad0-9) map to the same slot. An unassigned slot is left unhandled so the key does
    // nothing at all, rather than appearing to work and silently clearing the board's filters.
    private void ApplyCustomFilterShortcut(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        var slot = key switch
        {
            >= Key.D0 and <= Key.D9 => key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
            _ => -1
        };

        if (slot < 0 || DataContext is not MainViewModel viewModel) return;

        if (viewModel.ApplyCustomFilter(slot))
        {
            e.Handled = true;
        }
    }
}
