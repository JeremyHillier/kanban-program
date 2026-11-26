using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace KanbanApp.Theming;

// Stops a window flashing white as it opens. Windows puts a window on the screen as soon as it is shown, before WPF has drawn
// anything in it, so for a moment it is an empty white box, long enough to see on a busy window such as Settings or Help.
// Each window is cloaked (shown as far as the program is concerned, but kept off the screen by Windows) until its first
// drawing is done, then uncovered complete. Registered once at startup, so every window gets it by being a window.
public static class WindowReveal
{
    private const int DwmwaCloak = 13;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static bool _registered;
    private static readonly HashSet<Window> Seen = [];

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        // A window is first sized before Windows shows it, and it has its handle by then.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnFirstSized));
    }

    private static void OnFirstSized(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Window window || !ReferenceEquals(e.OriginalSource, window) || !Seen.Add(window)) return;
        window.Closed += (_, _) => Seen.Remove(window);
        if (window.IsLoaded) return; // already on the screen: nothing to hide
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !Cloak(handle, true)) return;

        var uncovered = false;
        void Uncover()
        {
            if (uncovered) return;
            uncovered = true;
            Cloak(handle, false);
        }

        // Once the first drawing has reached the screen. The timer only makes sure a window can never stay hidden.
        window.ContentRendered += (_, _) => window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, Uncover);
        var fallback = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher) { Interval = TimeSpan.FromSeconds(2) };
        fallback.Tick += (_, _) =>
        {
            fallback.Stop();
            Uncover();
        };
        fallback.Start();
        window.Closed += (_, _) => fallback.Stop();
    }

    private static bool Cloak(IntPtr handle, bool on)
    {
        try
        {
            var value = on ? 1 : 0;
            return DwmSetWindowAttribute(handle, DwmwaCloak, ref value, sizeof(int)) == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }
}
