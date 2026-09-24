using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KanbanApp.Views;

// Gives a text box a memory: past answers drop down under it and narrow as you type, and the best
// match fills itself in ahead of the cursor (selected, so typing on simply replaces it).
//
//   Typing        narrows the list; the first answer that starts with what is typed completes inline.
//   Down / Up     walk the list, putting each answer in the box.
//   Click         takes that answer.
//   Enter         is left alone, so the window's default button saves whatever is in the box.
//   Esc           closes the list first; a second Esc reaches the window as usual.
//   Shift+Delete  on a highlighted answer forgets it (when the owner allows that).
//
// The list never takes keyboard focus - the caret stays in the box throughout.
//
// Two ways to show the list. As a drop-down under the box (the default), which suits a field on a
// form: it appears while typing and goes when the box loses focus. Or inside a panel the window
// provides (inlineHost), which suits a small one-question prompt: a drop-down there opens over the
// prompt's own buttons and blocks them. The inline list is always shown, keeps one height while
// typing so the buttons below it never move under the mouse, and leaves Esc to the window.
internal sealed class TextBoxSuggestions
{
    private const int MaxShown = 8;

    private readonly TextBox _box;
    private readonly List<string> _all;
    private readonly Action<string>? _forget;
    private readonly bool _openWhenEmpty;
    private readonly Popup? _popup;       // null when the list sits inside the window
    private readonly Border _frame;
    private readonly ListBox _list;
    private bool _updating;
    private string? _userTyped;

    private bool IsInline => _popup is null;

    private TextBoxSuggestions(TextBox box, IEnumerable<string> suggestions, Action<string>? forget, bool openWhenEmpty, Panel? inlineHost)
    {
        _box = box;
        _all = suggestions.ToList();
        _forget = forget;
        _openWhenEmpty = openWhenEmpty;

        _list = new ListBox { Focusable = false, MaxHeight = 190, BorderThickness = new Thickness(0), Padding = new Thickness(0, 2, 0, 2) };
        _list.SetResourceReference(Control.BackgroundProperty, "InputBackgroundBrush");
        _list.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(UIElement.FocusableProperty, false));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 3, 8, 3)));
        _list.ItemContainerStyle = itemStyle;
        _list.PreviewMouseLeftButtonUp += List_PreviewMouseLeftButtonUp;

        _frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = _list, Margin = new Thickness(0, 2, 0, 0) };
        _frame.SetResourceReference(Border.BorderBrushProperty, "CardOutlineBrush");
        _frame.SetResourceReference(Border.BackgroundProperty, "InputBackgroundBrush");

        if (inlineHost is null)
        {
            _popup = new Popup { PlacementTarget = box, Placement = PlacementMode.Bottom, StaysOpen = true, AllowsTransparency = true, Child = _frame };
        }
        else
        {
            inlineHost.Children.Add(_frame);
            ShowAllAndHoldHeight();
        }

        box.TextChanged += Box_TextChanged;
        box.PreviewKeyDown += Box_PreviewKeyDown;
        box.GotKeyboardFocus += (_, _) => OpenIfEmpty();
        box.LostKeyboardFocus += (_, _) => Close();
        box.Unloaded += (_, _) => Close();
        box.Loaded += (_, _) =>
        {
            if (Window.GetWindow(box) is not { } window) return;
            window.Deactivated += (_, _) => Close();
            window.LocationChanged += (_, _) => Close();
            window.ContentRendered += (_, _) => OpenIfEmpty(); // focus may have arrived before the window was on screen
        };
    }

    // Nothing to suggest means nothing is attached: the box behaves exactly as before.
    // openWhenEmpty: show the recent answers as soon as the empty box has the cursor - right for a
    // one-question prompt, too busy for one field among many on a form.
    // inlineHost: show the list inside this panel rather than as a drop-down (see above).
    public static TextBoxSuggestions? Attach(TextBox box, IEnumerable<string> suggestions, Action<string>? forget = null, bool openWhenEmpty = false,
        Panel? inlineHost = null)
    {
        var list = suggestions.ToList();
        return list.Count == 0 ? null : new TextBoxSuggestions(box, list, forget, openWhenEmpty, inlineHost);
    }

    // After the list has been managed elsewhere: carry on with the new answers.
    public void Replace(IEnumerable<string> suggestions)
    {
        _all.Clear();
        _all.AddRange(suggestions);
        if (IsInline) ShowAllAndHoldHeight();
        else Close();
    }

    // The inline list starts with the recent answers and keeps the height that takes, so it doesn't
    // shrink and grow (moving the buttons under it) as typing narrows it.
    private void ShowAllAndHoldHeight()
    {
        _frame.Height = double.NaN;
        _list.ItemsSource = Matches(_all, _box.Text);
        _list.SelectedIndex = -1;
        _frame.Dispatcher.BeginInvoke(() =>
        {
            _frame.Height = _frame.ActualHeight > 0 ? _frame.ActualHeight : double.NaN;
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    internal bool IsOpen => _popup?.IsOpen ?? _list.Items.Count > 0;
    internal IReadOnlyList<string> Showing => _list.Items.Cast<string>().ToList();

    // Answers that start with what is typed come first, then ones that merely contain it; each
    // group keeps the order it was given in (most recent first). An exact match alone isn't worth a list.
    internal static List<string> Matches(IReadOnlyList<string> all, string typed)
    {
        var text = typed.Trim();
        if (text.Length == 0) return all.Take(MaxShown).ToList();

        var matches = all.Where(s => s.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .Concat(all.Where(s => !s.StartsWith(text, StringComparison.OrdinalIgnoreCase) && s.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxShown)
            .ToList();
        return matches.Count == 1 && string.Equals(matches[0], text, StringComparison.OrdinalIgnoreCase) ? [] : matches;
    }

    // The answer to complete inline: the first that starts with what is typed and goes further.
    internal static string? Completion(IReadOnlyList<string> all, string typed) =>
        typed.Length == 0 || typed != typed.TrimStart() ? null
        : all.FirstOrDefault(s => s.Length > typed.Length && s.StartsWith(typed, StringComparison.OrdinalIgnoreCase));

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;

        // While a guess is showing, the box holds the guess's capitals ("Sam's quote" for a typed
        // "sa"). _userTyped keeps what was really typed, so turning the guess down - by typing
        // something else, or deleting it - gives that back exactly.
        var added = e.Changes.Any(c => c.AddedLength > 0);
        var typed = _box.Text;
        if (_userTyped is not null && typed.Length >= _userTyped.Length && typed.StartsWith(_userTyped, StringComparison.OrdinalIgnoreCase))
        {
            typed = _userTyped + typed[_userTyped.Length..];
        }

        // Complete only when characters were typed at the end - never while deleting, which would
        // put back what was just removed.
        var typedAtEnd = added && _box.CaretIndex == _box.Text.Length && _box.SelectionLength == 0;
        if (typedAtEnd && _box.IsKeyboardFocused && Completion(_all, typed) is { } completion)
        {
            _userTyped = typed;
            _updating = true;
            _box.Text = completion;
            _box.Select(typed.Length, completion.Length - typed.Length);
            _updating = false;
        }
        else
        {
            if (typed != _box.Text) SetText(typed);
            _userTyped = typed.Length == 0 ? null : typed;
        }

        if (_box.IsKeyboardFocused) Refresh(typed);
    }

    private void Refresh(string? typed = null)
    {
        var matches = Matches(_all, typed ?? _box.Text);
        _list.ItemsSource = matches;
        _list.SelectedIndex = -1;
        if (_popup is null) return; // the inline list is simply there, with whatever matches

        _popup.MinWidth = _box.ActualWidth;
        _popup.MaxWidth = Math.Max(_box.ActualWidth, 420);
        _popup.IsOpen = matches.Count > 0;
    }

    // A drop-down closes; the inline list stays put and just drops its highlight.
    private void Close()
    {
        if (_popup is not null) _popup.IsOpen = false;
        else _list.SelectedIndex = -1;
    }

    private void OpenIfEmpty()
    {
        if (_openWhenEmpty && _box.IsKeyboardFocused && _box.IsVisible && _box.Text.Length == 0) Refresh();
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down or Key.Up:
                if (!IsOpen)
                {
                    if (e.Key == Key.Down) Refresh(string.Empty);
                    if (!IsOpen) return;
                }

                Move(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;

            case Key.Escape when _popup is { IsOpen: true }: // inline: Esc is the window's (Cancel)
                Close();
                e.Handled = true;
                break;

            case Key.Delete when IsOpen && _forget is not null && Keyboard.Modifiers == ModifierKeys.Shift && _list.SelectedItem is string highlighted:
                _forget(highlighted);
                _all.RemoveAll(s => string.Equals(s, highlighted, StringComparison.OrdinalIgnoreCase));
                TakeAnswer(string.Empty);
                Refresh();
                e.Handled = true;
                break;

            case Key.Enter or Key.Tab:
                Close(); // not handled: Enter goes on to the window's default button
                break;
        }
    }

    private void Move(int step)
    {
        var count = _list.Items.Count;
        if (count == 0) return;

        var index = _list.SelectedIndex < 0 ? (step > 0 ? 0 : count - 1) : (_list.SelectedIndex + step + count) % count;
        _list.SelectedIndex = index;
        _list.ScrollIntoView(_list.SelectedItem);
        TakeAnswer((string)_list.SelectedItem);
    }

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not ListBoxItem)
        {
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        if (element is not ListBoxItem { Content: string chosen }) return;

        TakeAnswer(chosen);
        Close();
        _box.Focus();
        e.Handled = true;
    }

    private void TakeAnswer(string text)
    {
        _userTyped = null; // a chosen answer is kept as it is written
        SetText(text);
    }

    private void SetText(string text)
    {
        _updating = true;
        _box.Text = text;
        _box.CaretIndex = text.Length;
        _updating = false;
    }
}
