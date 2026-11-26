using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace KanbanApp.Views;

// Alt+letter presses a button, with the letter underlined, as Windows dialogs do.
//
// Done here rather than with WPF's own access keys (a "_" in Content that the button template
// recognises): those also fire on the bare letter whenever focus is not in a text box, so a stray
// "d" typed at a list could press Delete. Here Alt is always required.
//
// A window marks its buttons in XAML with "_" before the letter ("_Rename..."). Register, at startup, binds every window
// as it loads; a window that sets labels in code may also call Bind itself after InitializeComponent. Bind strips the "_",
// underlines the letter, and takes over Alt+letter for that window. Buttons without a "_" are left alone, and a second
// Bind finds nothing left to do.
public static class AccessKeys
{
    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is Window window) Bind(window);
        }));
    }

    public static void Bind(Window window)
    {
        var map = new Dictionary<Key, List<Button>>();
        foreach (var button in Buttons(window))
        {
            if (button.Content is not string label) continue;
            var at = label.IndexOf('_');
            if (at < 0 || at == label.Length - 1) continue;

            var letter = label[at + 1];
            var plain = label.Remove(at, 1);
            if (!char.IsLetter(letter) || !Enum.TryParse<Key>(char.ToUpperInvariant(letter).ToString(), out var key))
            {
                button.Content = plain;
                continue;
            }

            button.Content = Underlined(plain, at);
            if (!map.TryGetValue(key, out var marked)) map[key] = marked = [];
            marked.Add(button);
        }

        if (map.Count == 0) return;

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.System || Keyboard.Modifiers != ModifierKeys.Alt) return;
            // One letter can mark a button on each tab of a window: the one that can be seen is pressed.
            var button = map.TryGetValue(e.SystemKey, out var buttons) ? buttons.FirstOrDefault(b => b.IsEnabled && b.IsVisible) : null;
            if (button is null) return;

            Press(button);
            e.Handled = true;
        };
    }

    // A label for a button whose text is set in code: "_" marks the letter, as in XAML.
    public static TextBlock Label(string labelWithUnderscore)
    {
        var at = labelWithUnderscore.IndexOf('_');
        return at < 0 ? new TextBlock { Text = labelWithUnderscore } : Underlined(labelWithUnderscore.Remove(at, 1), at);
    }

    private static TextBlock Underlined(string text, int at)
    {
        var block = new TextBlock();
        if (at > 0) block.Inlines.Add(new Run(text[..at]));
        block.Inlines.Add(new Run(text[at..(at + 1)]) { TextDecorations = TextDecorations.Underline });
        if (at + 1 < text.Length) block.Inlines.Add(new Run(text[(at + 1)..]));
        return block;
    }

    private static void Press(Button button)
    {
        if (new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke) invoke.Invoke();
    }

    private static IEnumerable<Button> Buttons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is not DependencyObject node) continue;
            if (node is Button button) yield return button;
            foreach (var inner in Buttons(node)) yield return inner;
        }
    }
}
