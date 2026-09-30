using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KanbanApp.Services;

// What an attachment's right-click menu does with the file itself: copy it to the clipboard, show
// it in its folder, and save a copy elsewhere. Kept apart from the window so it can be tested.
public static class AttachmentFiles
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp"];

    // The file as Explorer would copy it, so it pastes into a folder, an email or a chat. A picture
    // also goes on as a picture, for pasting straight into Word, Paint or an image editor.
    public static DataObject BuildClipboardData(string path)
    {
        var data = new DataObject();
        data.SetFileDropList(new StringCollection { path });

        if (ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // read now, so the file isn't held open
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                data.SetImage(bitmap);
            }
            catch
            {
                // Not a readable picture after all - the file itself is still on the clipboard.
            }
        }

        return data;
    }

    // The clipboard can be briefly held by another program; a couple of quick retries cover that.
    public static void CopyToClipboard(DataObject data)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 4)
            {
                Thread.Sleep(60);
            }
        }
    }

    // Files Windows runs, or that can start something, rather than open in a program: programs and
    // scripts, installers, shortcuts, and files that change settings. A task file can come from
    // someone else, and an attachment is only a path, so one of these is asked about before opening.
    private static readonly HashSet<string> ProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".scr", ".pif", ".cpl", ".msc", ".gadget",
        ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".ps1xml", ".ps2", ".ps2xml", ".psc1", ".psc2",
        ".vb", ".vbs", ".vbe", ".js", ".jse", ".ws", ".wsf", ".wsc", ".wsh", ".sct", ".hta", ".jar",
        ".msi", ".msp", ".mst", ".appx", ".appxbundle", ".msix", ".msixbundle", ".application", ".appref-ms",
        ".lnk", ".url", ".scf", ".shb", ".shs", ".settingcontent-ms", ".library-ms", ".searchconnector-ms",
        ".reg", ".inf", ".chm", ".diagcab", ".xll",
    };

    // Windows ignores dots and spaces at the end of a name, so "run.bat." still runs; a colon past the
    // drive letter names a hidden part of a file, which is no place for an ordinary attachment.
    public static bool CanRunAProgram(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length > 2 && trimmed.IndexOf(':', 2) >= 0) return true;
        return ProgramExtensions.Contains(Path.GetExtension(Path.GetFileName(trimmed).TrimEnd('.', ' ')));
    }

    // Asked before opening one. Enter and Esc both leave it unopened.
    public static DialogMessage ProgramQuestion(string path) =>
        DialogMessage.AskDanger("Open Program?",
            "This attachment is a program, or a file that can start one. Open it anyway?\n\n" +
            "Opening it runs it on this computer. Only go ahead if you know what it is and trust where it came from, " +
            "especially in a task file someone else shared.",
            "Open Anyway", "Don't Open") with { Detail = path };

    public static void ShowInFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    // The Save dialog's type list: this file's own type first, then anything.
    public static string SaveFilter(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrEmpty(extension)
            ? "All Files (*.*)|*.*"
            : $"{extension.TrimStart('.').ToUpperInvariant()} File (*{extension})|*{extension}|All Files (*.*)|*.*";
    }

    // False when the chosen place is the attachment itself, where there is nothing to do.
    public static bool SaveCopy(string source, string destination)
    {
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return false;

        File.Copy(source, destination, overwrite: true); // the Save dialog has already asked about replacing
        return true;
    }
}
