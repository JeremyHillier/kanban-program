using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace KanbanApp.Theming;

// Keeps every window inside the screen it is on, so its buttons can never be pushed off the bottom
// (a small laptop screen, a large font size, or a window that was laid out on a bigger monitor).
//
// Done once as a class handler, like DialogCopyright, so every window gets it, including any added
// later: on Loaded the window's MaxHeight and MaxWidth are capped at the working area of its own
// monitor (in WPF units, so a 150% display is handled), and a window that already runs past the
// edge is moved back in. A window whose content is taller than that must put the content in a
// ScrollViewer with its buttons outside it - WindowLayoutTests checks the XAML for that.
public static class WindowFit
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window) Fit(window);
    }

    // The window's own monitor rather than the primary one, and its work area rather than the
    // whole screen, so the taskbar never hides the bottom of a window.
    public static void Fit(Window window)
    {
        if (window.WindowState == WindowState.Maximized) return;

        var area = WorkAreaOf(window);
        if (area.Width <= 0 || area.Height <= 0) return;

        window.MaxHeight = Math.Max(window.MinHeight, area.Height);
        window.MaxWidth = Math.Max(window.MinWidth, area.Width);

        // Only once the window has a size: a SizeToContent window is measured before Loaded, so
        // ActualHeight is real here.
        var height = double.IsNaN(window.ActualHeight) ? 0 : window.ActualHeight;
        var width = double.IsNaN(window.ActualWidth) ? 0 : window.ActualWidth;
        if (height <= 0 || width <= 0) return;

        var left = window.Left;
        var top = window.Top;
        if (top + height > area.Bottom) top = area.Bottom - height;
        if (left + width > area.Right) left = area.Right - width;
        if (top < area.Top) top = area.Top;
        if (left < area.Left) left = area.Left;
        if (Math.Abs(top - window.Top) > 0.5) window.Top = top;
        if (Math.Abs(left - window.Left) > 0.5) window.Left = left;
    }

    // In WPF units. Falls back to the primary screen's work area when the window has no handle yet.
    internal static Rect WorkAreaOf(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MonitorDefaultToNearest);
                var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
                if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    var work = info.Work;
                    var pixels = new Rect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);
                    var source = PresentationSource.FromVisual(window);
                    if (source?.CompositionTarget is { } target)
                    {
                        return Rect.Transform(pixels, target.TransformFromDevice);
                    }
                    return pixels;
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Not Windows as expected; use what WPF knows.
        }

        return SystemParameters.WorkArea;
    }

    private static class NativeMethods
    {
        public const uint MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct RectStruct { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public int Size;
            public RectStruct Monitor;
            public RectStruct Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
