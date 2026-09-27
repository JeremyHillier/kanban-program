using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Win32;

namespace KanbanApp.Views;

// The date pickers: Today and Clear, the right-click "days from today" choices, and the line
// saying whether a saved view will keep "today" (moving with the calendar) or a fixed date.
public partial class ReportBuilderWindow
{
    // A date picker can hold a day that means "today" (its Today button, or a saved view's
    // "today+7"). The picker shows the real day, and the relative text is kept beside it in Tag
    // for as long as the picker still shows that day - so re-saving a loaded view keeps it
    // relative, while picking another day by hand makes it a fixed date again.
    private void SetDate(DatePicker picker, string? saved)
    {
        picker.SelectedDate = RelativeDate.Resolve(saved, DateTime.Today);
        picker.Tag = RelativeDate.IsRelative(saved) ? saved!.Trim() : null;
        if (picker == DueFromDatePicker || picker == DueToDatePicker) RefreshDateKind();
    }

    private static string? SavedDate(DatePicker picker)
    {
        if (picker.SelectedDate is not { } day) return null;
        return picker.Tag is string relative && RelativeDate.Resolve(relative, DateTime.Today) == day.Date ? relative : day.ToString("yyyy-MM-dd");
    }

    // The line under the From/To pickers. A picker's Tag holds the relative text ("today+7") while
    // it still shows that day (see SetDate/SavedDate); otherwise what it shows is a fixed date.
    private void RefreshDateKind()
    {
        if (DateKindText is null) return;
        string Kind(DatePicker picker) => picker.SelectedDate is null ? "any"
            : SavedDate(picker) is { } saved && RelativeDate.IsRelative(saved) ? $"{RelativeDate.Describe(saved)} (moves with the calendar)"
            : $"fixed {picker.SelectedDate:MMM d, yyyy}";
        DateKindText.Text = DueFromDatePicker.SelectedDate is null && DueToDatePicker.SelectedDate is null
            ? "A saved view keeps 'today' from the Today buttons as the word today, so it moves with the calendar. A date picked from the calendar is kept as that date."
            : $"Saved as: From {Kind(DueFromDatePicker)}, To {Kind(DueToDatePicker)}.";
    }

    private void DatePicker_Loaded(object sender, RoutedEventArgs e)
    {
        var picker = (DatePicker)sender;
        CalendarWheelSupport.Attach(picker);
        if (picker == DueFromDatePicker || picker == DueToDatePicker)
        {
            picker.SelectedDateChanged += (_, _) => RefreshDateKind();
            RefreshDateKind();
        }
    }

    private void TodayDueFrom_Click(object sender, RoutedEventArgs e) => SetDate(DueFromDatePicker, RelativeDate.Today);

    private void TodayDueTo_Click(object sender, RoutedEventArgs e) => SetDate(DueToDatePicker, RelativeDate.Today);

    private void ClearDueFrom_Click(object sender, RoutedEventArgs e) => SetDate(DueFromDatePicker, null);

    private void ClearDueTo_Click(object sender, RoutedEventArgs e) => SetDate(DueToDatePicker, null);

    // Right-click on a Today button: a day so many days from today, kept relative in the same way.
    private void TodayDueFrom_RightClick(object sender, MouseButtonEventArgs e) => OfferRelativeDays((Button)sender, DueFromDatePicker, e);

    private void TodayDueTo_RightClick(object sender, MouseButtonEventArgs e) => OfferRelativeDays((Button)sender, DueToDatePicker, e);

    private void OfferRelativeDays(Button button, DatePicker picker, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var days in new[] { -30, -14, -7, -1, 1, 7, 14, 30, 60, 90 })
        {
            var item = new MenuItem { Header = days < 0 ? $"{-days} day{(days == -1 ? "" : "s")} ago" : $"{days} day{(days == 1 ? "" : "s")} from today" };
            var captured = days;
            item.Click += (_, _) => SetDate(picker, RelativeDate.Relative(captured));
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void TodayArchivedFrom_Click(object sender, RoutedEventArgs e) => ArchivedFromDatePicker.SelectedDate = DateTime.Today;

    private void TodayArchivedTo_Click(object sender, RoutedEventArgs e) => ArchivedToDatePicker.SelectedDate = DateTime.Today;

    private void ClearArchivedFrom_Click(object sender, RoutedEventArgs e) => ArchivedFromDatePicker.SelectedDate = null;

    private void ClearArchivedTo_Click(object sender, RoutedEventArgs e) => ArchivedToDatePicker.SelectedDate = null;
}
