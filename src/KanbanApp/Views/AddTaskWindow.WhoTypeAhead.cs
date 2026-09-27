using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Typing in the Who picker: start typing a name, with the list open or with just the button
// focused, and the matching person's tickbox is brought into view and focused, ready for Space to
// tick it, however soon after the typing. Letters typed within a second of each other build on
// one another ("sa", then "m").
public partial class AddTaskWindow
{
    private string _whoTyped = string.Empty;
    private DateTime _whoTypedAt = DateTime.MinValue;

    private bool WhoTypingIsLive => _whoTyped.Length > 0 && DateTime.Now - _whoTypedAt < TypeAheadMatcher.ResetAfter;

    private void InitializeWhoTypeAhead()
    {
        WhoButton.PreviewTextInput += Who_PreviewTextInput;
        WhoPanel.PreviewTextInput += Who_PreviewTextInput;
        WhoButton.PreviewKeyDown += Who_PreviewKeyDown;
        WhoPanel.PreviewKeyDown += Who_PreviewKeyDown;
    }

    private void Who_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]) || char.IsWhiteSpace(e.Text[0])) return;

        WhoTypeAhead(e.Text);
        e.Handled = true;
    }

    private void Who_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            // Space always ticks the highlighted person, even straight after typing (it is never
            // taken as part of a name - a surname can be typed by itself instead). Ticked here
            // rather than left to the tickbox: its own Space handling takes and releases the mouse
            // capture, which can close a list that closes on an outside click. The next letter
            // typed starts a new search.
            case Key.Space when e.OriginalSource is CheckBox { Tag: PersonViewModel } box:
                _whoTyped = string.Empty;
                box.IsChecked = box.IsChecked != true;
                e.Handled = true;
                break;

            // Both would otherwise reach the window's Save and Cancel buttons and close the whole task.
            case Key.Escape or Key.Enter when WhoPopup.IsOpen:
                WhoButton.IsChecked = false;
                WhoButton.Focus();
                e.Handled = true;
                break;
        }
    }

    private void WhoTypeAhead(string text)
    {
        _whoTyped = WhoTypingIsLive ? _whoTyped + text : text;
        _whoTypedAt = DateTime.Now;

        var boxes = WhoBoxes();
        var index = TypeAheadMatcher.Match(boxes.Select(b => ((PersonViewModel)b.Tag).Name).ToList(), _whoTyped);
        if (index < 0 && _whoTyped.Length > 1)
        {
            // No name goes on like that: treat the newest letter as the start of a fresh search.
            _whoTyped = text;
            index = TypeAheadMatcher.Match(boxes.Select(b => ((PersonViewModel)b.Tag).Name).ToList(), _whoTyped);
        }
        if (index < 0) return;

        var target = boxes[index];
        if (WhoPopup.IsOpen)
        {
            FocusWhoBox(target);
        }
        else
        {
            // The list has to be on screen before anything in it can take the focus.
            WhoButton.IsChecked = true;
            Dispatcher.BeginInvoke(() => FocusWhoBox(target), DispatcherPriority.Input);
        }
    }

    private List<CheckBox> WhoBoxes() =>
        WhoPanel.Children.OfType<DockPanel>().SelectMany(row => row.Children.OfType<CheckBox>()).ToList();

    private static void FocusWhoBox(CheckBox box)
    {
        box.BringIntoView();
        box.Focus();
    }

    // Ticking a box rebuilds the list, which would drop the keyboard focus out of it.
    private PersonViewModel? FocusedWhoPerson() =>
        Keyboard.FocusedElement is CheckBox { Tag: PersonViewModel person } box && WhoBoxes().Contains(box) ? person : null;

    private void RestoreWhoFocus(PersonViewModel? person)
    {
        if (person is null) return;

        var box = WhoBoxes().FirstOrDefault(b => ((PersonViewModel)b.Tag).Id == person.Id);
        if (box is not null) Dispatcher.BeginInvoke(() => FocusWhoBox(box), DispatcherPriority.Input);
    }
}
