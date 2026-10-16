using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace KanbanApp.Services;

// A link to one task - kanbantask://task/<task ID>?file=<task file ID> - that can be put in an
// Outlook event or an email and opens the task in the app when it is clicked. The two IDs are
// random 32-digit hex numbers (a task's permanent ID, and the task file's own), so the link says
// nothing about the task, and one pointing into another task file is told apart from this one's.
//
// The app registers the link type for the signed-in user (no administrator rights) each time it
// starts. A link can only ask the app to open a task: nothing in it is ever run, and anything that
// is not exactly this shape is refused.
public static partial class TaskLinks
{
    // The Test build has its own, so the two never open each other's tasks.
    public static string Scheme => AppChannel.IsTest ? "kanbantask-test" : "kanbantask";

    public static string Build(string shareId, string fileId) => $"{Scheme}://task/{shareId}?file={fileId}";

    [GeneratedRegex(@"^(?<scheme>[a-z-]+)://task/(?<task>[0-9a-f]{32})/?\?file=(?<file>[0-9a-f]{32})/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    // The IDs in a link for this build, lower-cased; false for anything else.
    public static bool TryParse(string? text, out string shareId, out string fileId)
    {
        shareId = fileId = string.Empty;
        var match = Shape().Match((text ?? string.Empty).Trim().Trim('"'));
        if (!match.Success || !string.Equals(match.Groups["scheme"].Value, Scheme, StringComparison.OrdinalIgnoreCase)) return false;

        shareId = match.Groups["task"].Value.ToLowerInvariant();
        fileId = match.Groups["file"].Value.ToLowerInvariant();
        return true;
    }

    // Whether a start-up argument is one of this build's task links.
    public static bool IsLink(string? argument) => TryParse(argument, out _, out _);

    // --- Registering the link type with Windows --------------------------------------------------

    private static string KeyPath => $@"Software\Classes\{Scheme}";

    // What Windows will run for a link: this exe, with the link as its one argument.
    internal static string CommandFor(string exePath) => $"\"{exePath}\" \"%1\"";

    // Swapped for a dictionary by the tests, which must never touch the real registry.
    internal static Func<string?> ReadCommand = () =>
        Registry.CurrentUser.OpenSubKey($@"{KeyPath}\shell\open\command")?.GetValue(null) as string;

    internal static Action<string, string> WriteRegistration = (exePath, command) =>
    {
        using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
        {
            key.SetValue(null, $"URL:{AppChannel.DisplayName}");
            key.SetValue("URL Protocol", string.Empty);
        }
        using (var icon = Registry.CurrentUser.CreateSubKey($@"{KeyPath}\DefaultIcon")) icon.SetValue(null, $"\"{exePath}\",0");
        using (var open = Registry.CurrentUser.CreateSubKey($@"{KeyPath}\shell\open\command")) open.SetValue(null, command);
    };

    // Written each time the app starts, only when it is not already right: a reinstall or a move to
    // another folder puts it right by itself. Never throws - a link that does not open is not worth a crash.
    public static void Register()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            var command = CommandFor(exe);
            if (string.Equals(ReadCommand(), command, StringComparison.OrdinalIgnoreCase)) return;
            WriteRegistration(exe, command);
        }
        catch
        {
            // Registry locked down by policy: links simply will not open the app on this PC.
        }
    }

    // For the tests: a registry that is only a variable.
    internal static void UseInMemoryRegistry(out Func<string?> stored)
    {
        string? command = null;
        ReadCommand = () => command;
        WriteRegistration = (_, written) => command = written;
        stored = () => command;
    }
}
