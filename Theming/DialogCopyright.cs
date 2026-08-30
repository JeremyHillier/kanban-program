using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;

namespace KanbanApp.Theming;

// Stamps a subtle copyright line into the bottom-right of every dialog.
//
// Done once as a class handler rather than by pasting the same markup into ~17 window XAML files:
// those would drift the moment one was restyled, and any window added later would silently miss
// out. Registering against Window.Loaded catches every dialog, including future ones, with no
// per-window wiring.
public static class DialogCopyright
{
    // Windows that already show ownership as part of their own design - stamping a second copy
    // would just be duplication.
    private static readonly HashSet<string> ExcludedWindows =
    [
        "MainWindow",    // shows it under the sidebar's version label
        "SplashWindow",  // shows it centred, as part of the splash artwork
        "AboutWindow"    // shows it in full in the identity band, which is the dialog's whole point
    ];

    // Marker type: lets a second pass over the same window recognise its own work and skip it,
    // without resorting to a sentinel Tag that a window might legitimately want to use itself.
    private sealed class CopyrightHost : Grid;

    public static void Register()
    {
        // Loaded is a Direct routed event, so this fires for the Window itself only - never for a
        // child element's Loaded bubbling up through it.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window) return;
        if (ExcludedWindows.Contains(window.GetType().Name)) return;
        if (window.Content is not UIElement content || content is CopyrightHost) return;

        // Re-parent the window's existing root into row 0 rather than overlaying the footer on top
        // of it: every dialog here already puts its buttons bottom-right, and an overlay would sit
        // on them. A row of its own pushes the line cleanly underneath instead.
        var host = new CopyrightHost();
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        window.Content = null; // A UIElement can only have one parent; detach before re-parenting.
        Grid.SetRow(content, 0);
        host.Children.Add(content);

        var label = new TextBlock
        {
            Text = AppInfo.Copyright,
            FontSize = 9,
            // No opacity multiplier: SecondaryTextBrush is already a muted grey, and fading it
            // further washed the line out to near-invisible against a light background.
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 2, 10, 6),
            IsHitTestVisible = false // Never let it swallow a click meant for whatever sits above it.
        };
        // Resource reference, not a fixed brush, so the line follows a light/dark theme switch.
        label.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");

        Grid.SetRow(label, 1);
        host.Children.Add(label);

        window.Content = host;
    }
}
