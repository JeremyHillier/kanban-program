using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KanbanApp.Services;

// A task link was clicked while the app is already open: Windows starts a second copy with the link,
// and that copy hands the link to the open one (MainWindow.TaskLinks) and exits, rather than telling
// the user "already open". Only ever sends text; the open copy checks it is exactly a task link.
internal static class TaskLinkForwarder
{
    private const int WmCopyData = 0x004A;
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
        var bytes = System.Text.Encoding.Unicode.GetBytes(link + "\0");
        var text = Marshal.AllocHGlobal(bytes.Length);
        var data = Marshal.AllocHGlobal(Marshal.SizeOf<CopyData>());
        try
        {
            Marshal.Copy(bytes, 0, text, bytes.Length);
            Marshal.StructureToPtr(new CopyData { dwData = (IntPtr)KanbanApp.MainWindow.TaskLinkCopyDataId, cbData = bytes.Length, lpData = text }, data, false);

            AllowSetForegroundWindow(-1); // this copy was just started by a click, so it may let the open one come forward
            return SendMessageTimeout(window, WmCopyData, IntPtr.Zero, data, AbortIfHung, 5000, out var result) != IntPtr.Zero && result == 1;
        }
        finally
        {
            Marshal.FreeHGlobal(text);
            Marshal.FreeHGlobal(data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CopyData
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, int message, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);
}
