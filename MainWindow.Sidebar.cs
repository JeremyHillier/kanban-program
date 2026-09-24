using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// The button column's filters, sorting and view toggles, and its list-size grips.
public partial class MainWindow
{
    // The button column collapses to a thin strip and back: the Hide link, a click on the strip, or Alt+B.
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.ToggleSidebar();

    private void SidebarStrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        (DataContext as MainViewModel)?.ToggleSidebar();
        e.Handled = true;
    }

    // Rebuilt fresh on every hover rather than cached, so a slot saved or renamed a moment ago
    // (via the Manage Custom Filters dialog, or Alt+0-9 capture) always shows up-to-date without
    // needing an explicit refresh hook.
    private void CustomFiltersButton_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var defined = viewModel.CustomFilters
            .Select((filter, slot) => (filter, slot))
            .Where(x => x.filter.IsDefined)
            .ToList();

        CustomFiltersButton.ToolTip = defined.Count == 0
            ? "No custom filters saved yet. Set the board's filters how you like, then click here to save the combination to Alt+0 - Alt+9."
            : "Saved custom filters:\n" + string.Join("\n", defined.Select(x => $"Alt+{x.slot}: {x.filter.Name} — {x.filter.Summary}"));
    }

    private void WaitingOn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly(MainViewModel.WaitingOnFilter);
    }

    private void HideFuture_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ToggleHideFutureTasks();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLastAction();

    // FilterOptionViewModel.IsSelected is bound TwoWay to each ListBoxItem, so the Ctrl/Shift-click
    // selection itself is already applied to the view model by the time this fires - it only needs
    // to trigger the actual re-filter pass.
    private void ProjectFilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ApplyFilters();
    }

    private void PriorityFilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ApplyFilters();
    }

    private void WhoFilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.ApplyFilters();
    }

    private void SortByProject_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.Project, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    private void SortByDueDate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.DueDate, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    private void SortByWho_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.Who, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    private void SortByPriority_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ToggleSortKey(MainViewModel.SortKey.Priority, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    // Each of these clears every other filter before applying its own - see
    // MainViewModel.ShowDueFilterOnly for why they deliberately aren't cumulative.
    private void DueToday_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("Today");
    }

    private void DueTomorrow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("Tomorrow");
    }

    private void DueWithinWeek_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("Within a Week");
    }

    private void DueNone_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.ShowDueFilterOnly("No Due Date");
    }

    // The grip sits right under the list it resizes and moves with it, so each DragDelta is the
    // distance moved since the previous one and can simply be added on.
    private void FilterResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not FrameworkElement grip) return;

        if ((string)grip.Tag == "Project")
        {
            viewModel.ProjectFilterListHeight += e.VerticalChange;
        }
        else
        {
            viewModel.PriorityWhoFilterListHeight += e.VerticalChange;
        }
    }

    private void FilterResizeGrip_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        (DataContext as MainViewModel)?.SaveFilterListHeights();
    }

    private void FilterResizeGrip_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not FrameworkElement grip) return;

        if ((string)grip.Tag == "Project")
        {
            viewModel.ProjectFilterListHeight = MainViewModel.DefaultFilterListHeight;
        }
        else
        {
            viewModel.PriorityWhoFilterListHeight = MainViewModel.DefaultFilterListHeight;
        }
        viewModel.SaveFilterListHeights();
        e.Handled = true;
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearFilters();
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.ToggleTheme();
    }

    private void ToggleCardSize_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.ToggleCardSize();
    }

    // Shared by every XAML-declared DatePicker in this window (the due-date range filter's From/To
    // pickers) - the board's own due-date quick-edit popup above builds its DatePicker in code and
    // wires CalendarWheelSupport.Attach directly instead, since it has no XAML element to hang a
    // Loaded handler off of.
    private void DatePicker_Loaded(object sender, RoutedEventArgs e)
    {
        CalendarWheelSupport.Attach((System.Windows.Controls.DatePicker)sender);
    }
}
