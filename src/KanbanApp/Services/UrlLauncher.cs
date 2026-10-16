using System.Diagnostics;
using System.IO;
using System.Windows;

namespace KanbanApp.Services;

// Opens a task's link: a web address in the default browser, an email address in the mail app, or a
// file or folder on this PC or a network share. Shared by the task dialog's Link field and the About
// dialog's buttons, which hand over whatever the user typed/was configured - hence the scheme
// fix-up (people paste "example.com" far more often than "https://example.com", and ShellExecute
// treats a scheme-less string as a file path rather than a web address).
//
// A task file can come from someone else, and ShellExecute will happily run a program or trigger any
// registered protocol handler, so only three kinds of link are ever handed to Windows: web
// (http/https), email (mailto), and a plain Windows path (drive letter or \\server\share) that is
// not a program or script type. Anything else is refused here at launch time as well as when the
// task is saved or imported. A network path asks first: opening one tells that computer who you are.
public static class UrlLauncher
{
    public const string AllowedLinksMessage =
        "A task's link can be a web address (like example.com), an email address, or a file or folder on this PC or a network share. Programs and scripts cannot be used.";

    // What opens a program, runs a script, installs something, mounts an image or changes settings.
    // Compared without case, after Windows' own trailing dots and spaces are dropped.
    private static readonly HashSet<string> ProgramTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".scr", ".pif", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".ps1xml", ".ps2", ".vbs", ".vbe",
        ".js", ".jse", ".wsf", ".wsh", ".msi", ".msp", ".mst", ".hta", ".reg", ".cpl", ".inf", ".lnk", ".url",
        ".jar", ".dll", ".sys", ".ocx", ".application", ".gadget", ".appref-ms", ".msc", ".scf", ".chm", ".library-ms",
        ".search-ms", ".settingcontent-ms", ".diagcab", ".website", ".xll", ".wll", ".appx", ".msix", ".appxbundle",
        ".msixbundle", ".iso", ".img", ".vhd", ".vhdx", ".py", ".pyw", ".rb", ".pl", ".sh", ".msh", ".msh1", ".msh2",
        ".mshxml", ".msh1xml", ".msh2xml", ".ws", ".ade", ".adp", ".mde", ".mdb", ".accde", ".bas", ".cer", ".crt", ".der",
        ".ins", ".isp", ".job", ".lnk", ".mad", ".maf", ".mag", ".mam", ".maq", ".mar", ".mas", ".mat", ".mau", ".mav",
        ".maw", ".mda", ".mdt", ".mdw", ".mdz", ".ops", ".prf", ".prg", ".pst", ".shb", ".shs", ".tmp", ".vb", ".vsmacros",
        ".vsw", ".xnk"
    };

    public static void Open(string? url, Window? owner = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var cleaned = Clean(url);
        if (TryParsePath(cleaned, out var path))
        {
            OpenPath(path, owner);
            return;
        }

        if (!TryNormalize(cleaned, out var target))
        {
            Dialogs.Tell(owner, "Link Not Allowed", $"This link cannot be opened.\n\n{AllowedLinksMessage}", DialogTone.Warning, url.Trim());
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Tell(owner, "Could Not Open", "Windows could not open this link. There may be no web browser or email app set up.",
                DialogTone.Error, $"{target}\n\n{ex.Message}");
        }
    }

    private static void OpenPath(string path, Window? owner)
    {
        if (IsNetworkPath(path) && !Dialogs.Confirm(owner, DialogMessage.Ask("Open Network Location",
                "Open this network location?\n\nA link can point to another computer, and opening it tells that computer who you are. " +
                "Only continue if you know where the link goes.", "Open") with { Detail = path }))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                Dialogs.Tell(owner, "Not Found",
                    "That file or folder cannot be found.\n\nIt may have been moved or renamed, or it may be on another PC or a drive that is not connected.",
                    DialogTone.Warning, path);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Tell(owner, "Could Not Open", "Windows could not open this file or folder. There may be no app set up for this kind of file.",
                DialogTone.Error, $"{path}\n\n{ex.Message}");
        }
    }

    // What the user typed, tidied: Explorer's "Copy as path" wraps the path in quotes, and a link
    // copied from a browser or Office may be file:///C:/... - both become the plain path.
    public static string Clean(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"') trimmed = trimmed[1..^1].Trim();
        if (trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile && !string.IsNullOrEmpty(uri.LocalPath))
        {
            trimmed = uri.LocalPath;
        }
        return trimmed;
    }

    // The link as it is stored, or null when it is not one that may be used: a web or email address,
    // or a file or folder path that is not a program or script. Used when saving and importing.
    public static string? ValidLink(string? text)
    {
        var cleaned = Clean(text);
        if (cleaned.Length == 0) return null;
        return TryParsePath(cleaned, out _) || TryNormalize(cleaned, out _) ? cleaned : null;
    }

    public static bool IsPathLink(string? text) => TryParsePath(Clean(text), out _);

    // A Windows path on a drive (C:\Folder\file.pdf) or a network share (\\server\share\file.pdf).
    // Pure text rules, so a path that does not exist (yet, or on another PC) can still be saved.
    // Refused: device and WebDAV forms, stream names, wildcards, and the program and script types.
    internal static bool TryParsePath(string? text, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var candidate = text.Trim().Replace('/', '\\');
        if (candidate.Length is < 3 or > 400) return false;
        if (candidate.Any(c => char.IsControl(c) || c is '<' or '>' or '"' or '|' or '?' or '*')) return false;

        var drive = char.IsAsciiLetter(candidate[0]) && candidate[1] == ':' && candidate[2] == '\\';
        var share = candidate.StartsWith(@"\\", StringComparison.Ordinal);
        if (!drive && !share) return false;

        if (share)
        {
            var parts = candidate[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;                            // a server and a share, at least
            if (parts[0] is "?" or "." || parts[0].Contains('@')) return false; // \\?\ and \\.\ device paths, \\host@SSL WebDAV
            if (candidate.Contains(':')) return false;
        }
        else if (candidate.IndexOf(':', 2) >= 0)
        {
            return false;                                                  // C:\file.txt:stream
        }

        // Windows ignores trailing dots and spaces, so "run.exe." is still run.exe.
        var name = candidate.TrimEnd().TrimEnd('.', ' ');
        if (ProgramTypes.Contains(Path.GetExtension(name))) return false;

        path = candidate;
        return true;
    }

    public static bool IsNetworkPath(string path) => path.StartsWith(@"\\", StringComparison.Ordinal);

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

}
