using System.Windows;
using System.Windows.Controls;

namespace KanbanApp.Views;

// The task window's two tabs: Main (the essentials) and Secondary (everything else, and the custom fields). The tab strip is a
// real TabControl with no pages of its own; the two pages are scroll viewers under it, side by side in one cell, the one not
// chosen Hidden rather than Collapsed. Hidden keeps its room, so the window is as tall as the taller page and never jumps in
// size when the tab changes.
public partial class AddTaskWindow
{
    private void TaskTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, TaskTabs)) return;
        ShowSelectedPage();
    }

    // The page for the chosen tab. In the simple view there is only the Main page, and the Secondary one takes no room at all.
    private void ShowSelectedPage()
    {
        if (FormScrollViewer is null || SecondaryScrollViewer is null) return;

        var secondary = !_simplified && ReferenceEquals(TaskTabs.SelectedItem, SecondaryTab);
        FormScrollViewer.Visibility = secondary ? Visibility.Hidden : Visibility.Visible;
        SecondaryScrollViewer.Visibility = _simplified ? Visibility.Collapsed : secondary ? Visibility.Visible : Visibility.Hidden;
    }

    // A field on the Main tab that needs the user's attention (a time that is not one, a missing project).
    private void ShowMainTab()
    {
        TaskTabs.SelectedItem = MainTab;
        UpdateLayout();
    }

    // A field on the Secondary tab that needs the user's attention (a link that is not allowed, a number that is not one).
    private void ShowSecondaryTab()
    {
        TaskTabs.SelectedItem = SecondaryTab;
        UpdateLayout();
    }
}
