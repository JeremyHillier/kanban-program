using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// When: the Start and Due dates, the due time with its AM/PM buttons, and whether and how often
// the task repeats.
public partial class AddTaskWindow
{
    private void TodayDueDate_Click(object sender, RoutedEventArgs e)
    {
        DueDatePicker.SelectedDate = DateTime.Today;
    }

    private void ClearStartDate_Click(object sender, RoutedEventArgs e) => StartDatePicker.SelectedDate = null;

    private void ClearDueDate_Click(object sender, RoutedEventArgs e)
    {
        DueDatePicker.SelectedDate = null;
        _chosenPm = null;
        DueTimeTextBox.Text = string.Empty;
        UpdateMeridiemButtons();
    }

    // Set only by clicking AM/PM. Null means neither has been clicked, so DueTimeParser guesses.
    private bool? _chosenPm;

    private static readonly Brush MeridiemSelectedBrush = new SolidColorBrush(Color.FromRgb(0x0B, 0x5F, 0xD9));

    private string? CurrentDueTime() => DueTimeParser.Parse(DueTimeTextBox.Text, _chosenPm);

    private void DueTimeTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateMeridiemButtons();

    private void DueTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (CurrentDueTime() is { } parsed) DueTimeTextBox.Text = DueTimeParser.Format(parsed);
    }

    // With a time already in the box it's switched in place (2:30 PM -> 2:30 AM); with the box
    // empty or unreadable, the choice is kept for whatever gets typed next.
    private void Meridiem_Click(object sender, RoutedEventArgs e)
    {
        _chosenPm = sender == PmButton;
        if (CurrentDueTime() is { } parsed)
        {
            var time = TimeSpan.Parse(parsed);
            var hour = time.Hours % 12 + (_chosenPm == true ? 12 : 0);
            DueTimeTextBox.Text = DueTimeParser.Format($"{hour:00}:{time.Minutes:00}");
        }
        UpdateMeridiemButtons();
    }

    // Highlights the half of the day the typed time will actually save as (so a guess is visible
    // while typing), falling back to the clicked button when there's no readable time yet.
    private void UpdateMeridiemButtons()
    {
        bool? pm = CurrentDueTime() is { } time ? TimeSpan.Parse(time).Hours >= 12 : _chosenPm;
        StyleMeridiemButton(AmButton, pm == false);
        StyleMeridiemButton(PmButton, pm == true);
    }

    private static void StyleMeridiemButton(Button button, bool selected)
    {
        if (selected)
        {
            button.Background = MeridiemSelectedBrush;
            button.Foreground = Brushes.White;
        }
        else
        {
            button.SetResourceReference(BackgroundProperty, "ButtonBackgroundBrush");
            button.SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        }
    }


    private void RecurringCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RecurrenceComboBox.Visibility = RecurringCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // The times box takes digits only; anything pasted in is still checked on save.
    private void RecurrenceCountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    // Empty (or not recurring) means no end. False for anything that isn't a whole number from 1 to 999.
    private bool TryReadRecurrenceCount(out int? count)
    {
        count = null;
        var text = RecurrenceCountTextBox.Text.Trim();
        if (RecurringCheckBox.IsChecked != true || text.Length == 0) return true;

        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
            || value is < 1 or > 999) return false;

        count = value;
        return true;
    }
}
