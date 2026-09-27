using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Opens a window where it was last left (size, position and whether it was maximized) and saves
// that again when it closes. Call Restore before the window is shown. A window is never put
// somewhere the user can't see it: the remembered spot has to overlap a screen that is there now,
// and the size is capped at the screen area, since it may have come from a bigger monitor.
internal static class WindowPlacementMemory
{
    public static void Attach(Window window, MainViewModel viewModel, string key)
    {
        Restore(window, viewModel.WindowPlacement(key));
        window.Closing += (_, _) => Save(window, viewModel, key);
    }

    private static void Restore(Window window, (double Left, double Top, double Width, double Height, bool Maximized)? placement)
    {
        if (placement is not { } p) return;

        var area = SystemParameters.WorkArea;
        window.Width = Math.Clamp(p.Width, window.MinWidth, Math.Max(window.MinWidth, area.Width));
        window.Height = Math.Clamp(p.Height, window.MinHeight, Math.Max(window.MinHeight, area.Height));

        if (IsOnAScreen(p.Left, p.Top, window.Width))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = p.Left;
            window.Top = p.Top;
        }

        if (p.Maximized) window.WindowState = WindowState.Maximized;
    }

    // The same test the board uses for its own window: some of the top edge (the title bar) and
    // some of the width must land inside the desktop, across every monitor.
    internal static bool IsOnAScreen(double left, double top, double width) =>
        left + width > SystemParameters.VirtualScreenLeft
        && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top + 50 > SystemParameters.VirtualScreenTop
        && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    // A maximized window remembers the place it goes back to as well, so un-maximizing next time
    // doesn't leave it filling the screen.
    private static void Save(Window window, MainViewModel viewModel, string key)
    {
        var maximized = window.WindowState == WindowState.Maximized;
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;
        viewModel.SaveWindowPlacement(key, bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized);
    }
}
