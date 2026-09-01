using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KanbanApp.Services;

// Lets the mouse wheel page through months in a DatePicker's popup calendar - not built into WPF
// by default. An implicit Style targeting Calendar with an EventSetter (the obvious approach) does
// NOT work here: the Fluent theme's DatePicker template assigns its internal Calendar an explicit
// Style of its own, which bypasses normal type-based implicit style resolution entirely (confirmed
// via reflection - the live instance's Style never matched ours). Same class of Fluent-theme quirk
// documented elsewhere in App.xaml (ComboBox/ComboBoxItem/etc. needed full template overrides for
// the same underlying reason), but a Calendar's internal parts aren't exposed for a template
// override the way those were, so this instead finds the real, already-templated Calendar instance
// after the popup opens and attaches directly to it.
public static class CalendarWheelSupport
{
    public static void Attach(DatePicker datePicker)
    {
        datePicker.CalendarOpened += (_, _) =>
        {
            var calendar = FindOpenCalendar();
            if (calendar is null) return;

            // Guard against double-subscribing if the same Calendar instance is reused across
            // repeated opens of the same DatePicker (the common case).
            calendar.PreviewMouseWheel -= OnPreviewMouseWheel;
            calendar.PreviewMouseWheel += OnPreviewMouseWheel;
        };
    }

    private static Calendar? FindOpenCalendar()
    {
        foreach (var source in PresentationSource.CurrentSources)
        {
            if (source is not PresentationSource { RootVisual: DependencyObject root }) continue;

            var calendar = FindCalendar(root);
            if (calendar is not null) return calendar;
        }
        return null;
    }

    private static Calendar? FindCalendar(DependencyObject root)
    {
        if (root is Calendar calendar) return calendar;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindCalendar(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not Calendar calendar) return;

        // Scroll up (away from you) -> previous month; scroll down -> next month, matching how
        // scrolling up a page moves toward earlier content.
        calendar.DisplayDate = calendar.DisplayDate.AddMonths(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }
}
