using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace KanbanApp.Views;

// Where a report's PDF goes, for the Quick Report and the Report Builder alike: straight into the
// default export folder from Settings when it is set and still there, otherwise wherever the user
// picks in a Save dialog. The file is named after the report's title plus the time.
internal static class ReportPdfLocation
{
    // Null when the user cancels the Save dialog.
    public static string? Choose(Window owner, string defaultFolder, string title)
    {
        var fileName = FileName(title, DateTime.Now);

        var inDefaultFolder = InDefaultFolder(defaultFolder, fileName);
        if (inDefaultFolder is not null) return inDefaultFolder;

        var dialog = new SaveFileDialog
        {
            Title = "Save Report as PDF",
            Filter = "PDF File (*.pdf)|*.pdf",
            FileName = fileName
        };

        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    internal static string FileName(string title, DateTime now) =>
        $"{string.Join("_", title.Split(Path.GetInvalidFileNameChars()))}_{now:yyyyMMdd_HHmmss}.pdf";

    // Null when there is no usable default folder.
    internal static string? InDefaultFolder(string defaultFolder, string fileName) =>
        !string.IsNullOrWhiteSpace(defaultFolder) && Directory.Exists(defaultFolder) ? Path.Combine(defaultFolder, fileName) : null;
}
