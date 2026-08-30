using System.Diagnostics;
using System.Windows;

namespace KanbanApp.Services;

// Opens a URL in the user's default browser. Shared by the task dialog's Website field and the
// About dialog's Website button, both of which hand over whatever the user typed/was configured -
// hence the scheme fix-up (people paste "example.com" far more often than "https://example.com",
// and ShellExecute treats a scheme-less string as a file path rather than a web address).
public static class UrlLauncher
{
    public static void Open(string? url, Window? owner = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var target = url.Trim();
        if (!target.Contains("://")) target = "https://" + target;

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            var message = $"Couldn't open the website:\n\n{target}\n\n{ex.Message}";
            if (owner is not null)
            {
                MessageBox.Show(owner, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
