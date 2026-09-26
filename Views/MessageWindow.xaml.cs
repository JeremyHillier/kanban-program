using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanApp.Services;

namespace KanbanApp.Views;

// The app's message window, in place of the plain Windows message box: the point of the message in
// bold, paragraphs at a readable width, bullets that line up, a path or error in a box of its own,
// buttons named for what they do, and the app's own light or dark theme. Services/DialogMessage says
// how the text is laid out; Services/Dialogs shows it.
public partial class MessageWindow : Window
{
    public DialogChoice Choice { get; private set; }

    public MessageWindow(DialogMessage message)
    {
        InitializeComponent();
        Title = message.Title;
        (Mark.Background, MarkText.Text) = message.Tone switch
        {
            DialogTone.Question => (Brush("#FF3E6A9B"), "?"),
            DialogTone.Warning => (Brush("#FFC98A1B"), "!"),
            DialogTone.Error => (Brush("#FFC0392B"), "×"),
            _ => (Brush("#FF2B7BB9"), "i"),
        };
        Layout(message);

        YesButton.Content = message.Yes;
        if (message.IsDanger)
        {
            YesButton.Background = Brush("#FFC0392B");
            YesButton.Foreground = Brushes.White;
            YesButton.FontWeight = FontWeights.SemiBold;
        }
        NoButton.Content = message.No;
        NoButton.Visibility = message.No is null ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.Content = message.Cancel;
        CancelButton.Visibility = message.Cancel is null ? Visibility.Collapsed : Visibility.Visible;

        // Esc and the X: the safest way out. Enter: the main button, unless it does damage (then the safest
        // way out too, unless the message says the damage is undoable and Enter may stay on the main button).
        var safe = message.Cancel is not null ? CancelButton : message.No is not null ? NoButton : YesButton;
        var enter = message.IsDanger && !message.EnterChoosesMain ? safe : YesButton;
        enter.IsDefault = true;
        Choice = safe == CancelButton ? DialogChoice.Cancel : safe == NoButton ? DialogChoice.No : DialogChoice.Yes;
        Loaded += (_, _) => enter.Focus();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBox)
            {
                // As the Windows message box does: Ctrl+C copies the whole message.
                e.Handled = true;
                try { Clipboard.SetText($"{message.Title}\n\n{message}"); }
                catch (System.Runtime.InteropServices.ExternalException) { }
            }
        };
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    // The text as paragraphs: the first in bold, bullets with a hanging indent, then the detail box.
    private void Layout(DialogMessage message)
    {
        var paragraphs = message.Text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var p = 0; p < paragraphs.Length; p++)
        {
            // A paragraph's last piece is followed by the paragraph gap; text that leads into bullets, by a small one.
            var bottom = p == paragraphs.Length - 1 ? 0 : 10;
            var text = new List<string>();
            void FlushText(double after)
            {
                if (text.Count == 0) return;
                var block = new TextBlock
                {
                    Text = string.Join("\n", text),
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = p == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                    FontSize = p == 0 ? 14 : 13,
                    Margin = new Thickness(0, 0, 0, after),
                };
                block.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
                Body.Children.Add(block);
                text.Clear();
            }
            foreach (var line in paragraphs[p].Split('\n'))
            {
                if (!line.TrimStart().StartsWith("• ", StringComparison.Ordinal))
                {
                    text.Add(line.Trim());
                    continue;
                }
                FlushText(4);
                Body.Children.Add(Bullet(line.TrimStart()[2..].Trim()));
            }
            if (text.Count > 0) FlushText(bottom);
            else if (Body.Children[^1] is Grid lastBullet) lastBullet.Margin = new Thickness(6, 1, 0, bottom);
        }

        if (message.Detail is { Length: > 0 } detail)
        {
            var box = new TextBox
            {
                Text = detail,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 10, 0, 0),
                ToolTip = "Select it to copy it.",
            };
            box.SetResourceReference(BackgroundProperty, "InputBackgroundBrush");
            box.SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            box.SetResourceReference(BorderBrushProperty, "CardBorderBrush");
            Body.Children.Add(box);
        }
    }

    // One bullet: the mark in its own column, so a wrapped line lines up under the text.
    private static Grid Bullet(string text)
    {
        var grid = new Grid { Margin = new Thickness(6, 1, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = new TextBlock { Text = "•", FontSize = 13 };
        mark.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        grid.Children.Add(mark);
        var body = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        body.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);
        return grid;
    }

    private void Yes_Click(object sender, RoutedEventArgs e) => Finish(DialogChoice.Yes);
    private void No_Click(object sender, RoutedEventArgs e) => Finish(DialogChoice.No);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(DialogChoice.Cancel);

    private void Finish(DialogChoice choice)
    {
        Choice = choice;
        Close();
    }
}
