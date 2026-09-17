using System.Diagnostics;
using System.Windows;

namespace KanbanApp.Services;

// Opens a URL in the user's default browser. Shared by the task dialog's Website field and the
// About dialog's Website button, both of which hand over whatever the user typed/was configured -
// hence the scheme fix-up (people paste "example.com" far more often than "https://example.com",
// and ShellExecute treats a scheme-less string as a file path rather than a web address).
//
// Only web (http/https) and email (mailto) links are ever handed to Windows. A task file can come
// from someone else, and ShellExecute will happily run a program, open a file share, or trigger any
// registered protocol handler, so anything else is refused here at launch time as well as when the
// task is saved.
public static class UrlLauncher
{
    public const string AllowedLinksMessage =
        "Only web links (like example.com or https://example.com) and email addresses can be used as a task's website.";

    public static void Open(string? url, Window? owner = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!TryNormalize(url, out var target))
        {
            ShowError(owner, "Link Not Allowed", $"This link can't be opened:\n\n{url.Trim()}\n\n{AllowedLinksMessage}", MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError(owner, "Error", $"Couldn't open the website:\n\n{target}\n\n{ex.Message}", MessageBoxImage.Error);
        }
    }

    // Turns what the user typed into a link that's safe to hand to ShellExecute, or returns false.
    // "name@example.com" becomes a mailto: link, a bare "example.com" gets https:// in front.
    internal static bool TryNormalize(string? text, out string target)
    {
        target = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();
        if (trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or '\\')) return false;

        if (trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return TryMailto(trimmed["mailto:".Length..], out target);
        }

        if (!trimmed.Contains("://"))
        {
            if (trimmed.Contains('@') && !trimmed.Contains('/')) return TryMailto(trimmed, out target);
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (uri.IsUnc || uri.IsFile || string.IsNullOrEmpty(uri.Host) || (!uri.Host.Contains('.') && uri.Host != "localhost")) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

        target = uri.AbsoluteUri;
        return true;
    }

    private static bool TryMailto(string address, out string target)
    {
        target = string.Empty;
        var at = address.IndexOf('@');
        var queryAt = address.IndexOf('?');
        var mailbox = queryAt >= 0 ? address[..queryAt] : address;
        if (at <= 0 || at != mailbox.LastIndexOf('@') || at == mailbox.Length - 1) return false;
        if (!mailbox[(at + 1)..].Contains('.')) return false;
        if (mailbox.Any(c => c is '/' or ':' or '<' or '>' or '%' or '&')) return false;

        target = "mailto:" + address;
        return true;
    }

    private static void ShowError(Window? owner, string title, string message, MessageBoxImage icon)
    {
        if (owner is not null)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, icon);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
    }
}
