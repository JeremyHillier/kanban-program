using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KanbanApp.Services;

// A task link was clicked while the app is already open: Windows starts a second copy with the link,
// and that copy hands the link to the open one (MainWindow.TaskLinks) and exits, rather than telling
// the user "already open". Only ever sends text; the open copy checks it is exactly a task link.
internal static class TaskLinkForwarder
{
    private const uint AbortIfHung = 0x0002;

    // True when an open copy took the link.
    public static bool TrySend(string link)
    {
        using var me = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(me.ProcessName))
        {
            using (other)
            {
                if (other.Id == me.Id || other.MainWindowHandle == IntPtr.Zero) continue;
                if (Deliver(other.MainWindowHandle, link)) return true;
            }
        }
        return false;
    }

    private static bool Deliver(IntPtr window, string link)
    {
        var data = TaskLinkMessage.Allocate(link, out var text);
        try
        {
            AllowSetForegroundWindow(-1); // this copy was just started by a click, so it may let the open one come forward
            // The open copy answers 1 when it took the link; 0 (or no answer within 5 s) means try the next one.
            return SendMessageTimeout(window, TaskLinkMessage.WmCopyData, IntPtr.Zero, data, AbortIfHung, 5000, out var result) != IntPtr.Zero && result == 1;
        }
        finally
        {
            Marshal.FreeHGlobal(text);
            Marshal.FreeHGlobal(data);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, int message, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);
}
