using System.IO;
using System.Windows;
using KanbanApp.Services;

namespace KanbanApp.Views;

// Email a report as a PDF, from the Report Builder or Quick Report. The PDF is written to its own
// temp folder (no Save dialog: it is a copy for the email, not a file to keep), then the email opens
// with it attached and the To line empty for the user to fill. Same route as every other email in
// the app: classic Outlook attaches the file; otherwise the default mail app opens and a folder
// holding the PDF opens beside it to drag in. Nothing is sent until the user sends it.
internal static class ReportEmail
{
    private static readonly string FolderRoot = Path.Combine(Path.GetTempPath(), "Kanban Task Board Reports");
    internal const string DialogTitle = "Email Report";

    public static void Send(Window owner, string title, Action<string> writePdf)
    {
        string filePath;
        try
        {
            Directory.CreateDirectory(FolderRoot);
            filePath = Path.Combine(FolderRoot, ReportPdfLocation.FileName(title, DateTime.Now));
            writePdf(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.Tell(owner, DialogTitle, "The report could not be made into a PDF for the email.", DialogTone.Warning, ex.Message);
            return;
        }

        OutlookEmailHelper.ComposeEmailWithFiles(owner, string.Empty, title, Body(title, DateTime.Now), [filePath], "Report", DialogTitle);
    }

    internal static string Body(string title, DateTime now) =>
        $"Attached is the report \"{title}\", as of {now:MMMM d, yyyy}.";
}
