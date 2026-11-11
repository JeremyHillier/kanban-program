using System.Windows;
using System.Windows.Controls;

namespace KanbanApp.Views;

// The full form or the short one (project, goal, task, due date, recurring, who). The short one only hides the
// rest: every control keeps its value and is still read on save, so switching view, or editing a task that has
// notes, sub-tasks and so on in the short view, never loses or changes anything. Which view a window opens in is a
// setting (SimplifiedTaskWindow); the button changes this window only.
public partial class AddTaskWindow
{
    private bool _simplified;

    // Everything marked Tag="FullOnly" in the XAML.
    private IEnumerable<FrameworkElement> FullOnlyElements()
    {
        var found = new List<FrameworkElement>();
        void Walk(DependencyObject parent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                if (child is FrameworkElement { Tag: "FullOnly" } element) found.Add(element);
                Walk(child);
            }
        }
        Walk(FormScrollViewer);
        return found;
    }

    private void ApplyViewMode(bool simplified)
    {
        _simplified = simplified;

        foreach (var element in FullOnlyElements())
        {
            element.Visibility = simplified ? Visibility.Collapsed : Visibility.Visible;
        }

        // With Priority and Category and the start date and time gone, Who and Due take the room they left.
        Grid.SetColumn(WhoLabelRow, simplified ? 0 : 4);
        Grid.SetColumnSpan(WhoLabelRow, simplified ? 3 : 1);
        Grid.SetColumn(WhoGrid, simplified ? 0 : 4);
        Grid.SetColumnSpan(WhoGrid, simplified ? 3 : 1);
        Grid.SetColumn(DueGrid, simplified ? 0 : 2);
        Grid.SetColumnSpan(DueGrid, simplified ? 3 : 1);

        ViewModeButton.Content = simplified ? "Full View" : "Simple View";
        ViewModeButton.ToolTip = simplified
            ? "Show every field of the task. Nothing is lost by switching."
            : "Show only project, goal, task, due date, recurring and who. Other fields keep what they hold. Settings chooses which view this window starts in.";

        RefreshTemplatePanel();
    }

    private void ViewMode_Click(object sender, RoutedEventArgs e) => ApplyViewMode(!_simplified);
}
