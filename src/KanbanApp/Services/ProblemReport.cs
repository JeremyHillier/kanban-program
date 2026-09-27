using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace KanbanApp.Services;

// "Report a Problem": opens an email to support with the app's crash.log attached, so errors on a
// customer's PC reach us instead of sitting unread next to their task file. Nothing is sent
// automatically - the user sees the email, can describe what happened, and sends it themselves.
public static class ProblemReport
{
    public const string DialogTitle = "Report a Problem";

    // The log is appended to forever; only its most recent part is worth sending.
    internal const int MaxLogBytes = 512 * 1024;

    private static readonly string ReportFolderRoot = Path.Combine(Path.GetTempPath(), "Kanban Task Board Problem Report");

    public static string CrashLogPath(string dbPath) =>
        Path.Combine(Path.GetDirectoryName(dbPath) ?? Path.GetTempPath(), "crash.log");

    public static void Compose(Window? owner, string dbPath)
    {
        var logCopy = PrepareLogCopy(CrashLogPath(dbPath), ReportFolderRoot);
        var body = BuildBody(AppVersion, logCopy is not null, DateTime.Now);
        var files = logCopy is null ? Array.Empty<string>() : new[] { logCopy };

        OutlookEmailHelper.ComposeEmailWithFiles(owner, AppInfo.SupportEmail,
            $"{AppChannel.DisplayName} problem report (version {AppVersion})", body, files, "Problem Report", DialogTitle);
    }

    private static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";

    // Copies crash.log to its own temp folder (trimmed to the newest MaxLogBytes, starting at a
    // line boundary) so the attachment is a stable snapshot the app can't append to mid-send.
    // Returns null when there's no log, or it's empty.
    internal static string? PrepareLogCopy(string logPath, string rootDir)
    {
        try
        {
            if (!File.Exists(logPath)) return null;

            byte[] bytes;
            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var start = Math.Max(0, stream.Length - MaxLogBytes);
                stream.Seek(start, SeekOrigin.Begin);
                bytes = new byte[stream.Length - start];
                stream.ReadExactly(bytes);

                if (start > 0)
                {
                    var newline = Array.IndexOf(bytes, (byte)'\n');
                    if (newline >= 0) bytes = bytes[(newline + 1)..];
                }
            }

            if (bytes.Length == 0) return null;

            Directory.CreateDirectory(rootDir);
            var copy = Path.Combine(rootDir, "crash.log");
            File.WriteAllBytes(copy, bytes);
            return copy;
        }
        catch
        {
            // A report without the log is still worth sending.
            return null;
        }
    }

    internal static string BuildBody(string version, bool hasLog, DateTime now)
    {
        var sb = new StringBuilder();
        sb.Append("What were you doing when the problem happened? What did you expect, and what happened instead?\r\n\r\n\r\n\r\n");
        sb.Append("----------\r\n");
        sb.Append($"{AppChannel.DisplayName} version {version} ({AppChannel.Name})\r\n");
        sb.Append($"Windows: {Environment.OSVersion.VersionString}\r\n");
        sb.Append($".NET: {Environment.Version}\r\n");
        sb.Append($"Reported: {now:yyyy-MM-dd HH:mm}\r\n");
        sb.Append(hasLog
            ? "The app's error log (crash.log) is attached.\r\n"
            : "No error log was found on this PC.\r\n");
        return sb.ToString();
    }
}
