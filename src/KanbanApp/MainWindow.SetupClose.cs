using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KanbanApp.Views;

namespace KanbanApp;

// Setup, finding the app running, offers to close it. It asks through a registered window message
// (installer\KanbanTaskBoard.iss sends it to the main window by title) rather than WM_CLOSE, so
// the app can answer: it closes itself the normal way (backup and all) unless a task is being
// edited, in which case it refuses and Setup tells the user to finish the task first.
public partial class MainWindow
{
    // Shared with the installer script; both register it by this name.
    public const string CloseForSetupMessageName = "KanbanTaskBoard.CloseForSetup";

    // What the message returns to Setup.
    public const int CloseForSetupClosing = 1;
    public const int CloseForSetupRefused = 2;

    private static readonly uint CloseForSetupMessage = NativeSetupClose.RegisterWindowMessage(CloseForSetupMessageName);

    private void InitializeSetupCloseRequest()
    {
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(SetupCloseHook);
            Closed += (_, _) => source?.RemoveHook(SetupCloseHook);
        };
    }

    private IntPtr SetupCloseHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != CloseForSetupMessage) return IntPtr.Zero;
        handled = true;

        if (HasTaskWorkOpen()) return CloseForSetupRefused;

        // Not inside the message itself: closing tears the window down, and Setup is waiting on the reply.
        Dispatcher.BeginInvoke(() =>
        {
            foreach (Window owned in OwnedWindows.Cast<Window>().ToList()) owned.Close();
            Close();
        });
        return CloseForSetupClosing;
    }

    // A task being added, edited or reviewed: closing would throw that work away, so the app says no.
    internal static bool HasTaskWorkOpen() =>
        Application.Current?.Windows.Cast<Window>().Any(w => w.IsVisible && IsTaskWork(w)) == true;

    internal static bool IsTaskWork(Window window) => window switch
    {
        AddTaskWindow => true,
        ImportedTasksWindow => true,
        QuickAddWindow quickAdd => quickAdd.HasText,
        _ => false
    };

    private static class NativeSetupClose
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string name);
    }
}
