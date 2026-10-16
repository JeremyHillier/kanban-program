using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp;

// A link to a task (Services/TaskLinks), clicked in an Outlook event or an email, arrives here: from
// the copy of the app Windows just started (if none was running) or, if the app is already open, from
// that second copy as a WM_COPYDATA message, after which the second copy exits. Either way the board
// shows the task - its filters cleared if they were hiding it - highlights it and opens it. A link to a
// task that has since been archived or deleted says so instead.
public partial class MainWindow
{
    // Marks our own WM_COPYDATA, so a message from some other program is not mistaken for a link.
    internal const int TaskLinkCopyDataId = 0x4B54; // "KT"
    private const int WmCopyData = 0x004A;

    private void InitializeTaskLinkReceiver()
    {
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(TaskLinkHook);
            Closed += (_, _) => source?.RemoveHook(TaskLinkHook);
        };
    }

    private IntPtr TaskLinkHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmCopyData || lParam == IntPtr.Zero) return IntPtr.Zero;

        var data = Marshal.PtrToStructure<CopyData>(lParam);
        if (data.dwData != TaskLinkCopyDataId || data.cbData is <= 0 or > 1024 || data.lpData == IntPtr.Zero) return IntPtr.Zero;

        handled = true;
        var text = Marshal.PtrToStringUni(data.lpData, data.cbData / 2)?.TrimEnd((char)0);

        // Not inside the message: the sender is waiting on it, and opening a window can take a while.
        Dispatcher.BeginInvoke(() => OpenTaskLink(text));
        return (IntPtr)1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CopyData
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    // Shows the task a link names. Anything that is not a link for this build is ignored.
    internal void OpenTaskLink(string? link)
    {
        if (DataContext is not MainViewModel viewModel || !TaskLinks.TryParse(link, out var shareId, out var fileId)) return;

        BringToFront();

        if (!string.Equals(fileId, viewModel.TaskFileId, StringComparison.OrdinalIgnoreCase))
        {
            Dialogs.Tell(this, "Different Task File",
                "That link is to a task in another task file.\n\nIt can only be opened while the task file it came from is open.", DialogTone.Warning);
            return;
        }

        var found = viewModel.FindTask(shareId);
        if (found is null)
        {
            Dialogs.Tell(this, "Task Not Found",
                "That task is not in this task file.\n\nIt may have been deleted for good.", DialogTone.Warning);
            return;
        }

        if (!found.IsOnBoard)
        {
            var what = found.IsDeleted ? "deleted" : "archived";
            var when = found.PutAwayAt is { } at ? $" on {at:MMM d, yyyy}" : string.Empty;
            Dialogs.Tell(this, found.IsDeleted ? "Task Deleted" : "Task Archived",
                $"\"{found.Title}\" was {what}{when}.\n\n" +
                (found.IsDeleted ? "Open Deleted Tasks to bring it back." : "Open Archived Tasks to bring it back."), DialogTone.Info);
            return;
        }

        var card = viewModel.FindSharedCard(shareId);
        if (card is null) return;

        // Filters that would hide it are cleared, so the task can be seen where it sits.
        if (!card.IsVisible) viewModel.ClearFilters();
        viewModel.ClearCardSelection();
        card.IsSelected = true;
        EditCard(card, viewModel);
    }

    // Up from the taskbar or behind other windows; a window the user minimised comes back as it was.
    private void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true; // the flip brings it to the front once without pinning it there
        Topmost = false;
        Focus();
    }
}
