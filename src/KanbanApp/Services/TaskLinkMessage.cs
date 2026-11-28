using System.Runtime.InteropServices;

namespace KanbanApp.Services;

/// <summary>
/// The message one copy of the app sends another to hand over a clicked task link: a Windows WM_COPYDATA
/// carrying the link as UTF-16 text ending in a null. The sending side is <see cref="TaskLinkForwarder"/>,
/// the receiving side MainWindow.TaskLinks; both build and read it here so they cannot drift apart.
/// </summary>
internal static class TaskLinkMessage
{
    public const int WmCopyData = 0x004A;

    // Marks our own WM_COPYDATA, so a message from some other program is not mistaken for a link.
    public const int Id = 0x4B54; // "KT"

    // A link is short; anything over this is not ours and is ignored unread.
    private const int MaxBytes = 1024;

    /// <summary>
    /// Builds the message for <paramref name="link"/> in unmanaged memory and returns the pointer to pass
    /// as lParam. The caller frees both blocks with <see cref="Marshal.FreeHGlobal"/> once the send returns.
    /// </summary>
    public static IntPtr Allocate(string link, out IntPtr text)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(link + "\0");
        text = Marshal.AllocHGlobal(bytes.Length);
        var data = Marshal.AllocHGlobal(Marshal.SizeOf<CopyData>());
        Marshal.Copy(bytes, 0, text, bytes.Length);
        Marshal.StructureToPtr(new CopyData { dwData = (IntPtr)Id, cbData = bytes.Length, lpData = text }, data, false);
        return data;
    }

    /// <summary>
    /// Reads a WM_COPYDATA's lParam. False when it is not one of ours (wrong id, no text, or longer than a
    /// link can be). The text is cut at the stated length, and trailing nulls are dropped.
    /// </summary>
    public static bool TryRead(IntPtr lParam, out string? text)
    {
        text = null;
        if (lParam == IntPtr.Zero) return false;

        var data = Marshal.PtrToStructure<CopyData>(lParam);
        if (data.dwData != Id || data.cbData is <= 0 or > MaxBytes || data.lpData == IntPtr.Zero) return false;

        text = Marshal.PtrToStringUni(data.lpData, data.cbData / 2)?.TrimEnd((char)0);
        return true;
    }

    // Windows' COPYDATASTRUCT.
    [StructLayout(LayoutKind.Sequential)]
    private struct CopyData
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }
}
