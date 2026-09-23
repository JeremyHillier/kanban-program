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
