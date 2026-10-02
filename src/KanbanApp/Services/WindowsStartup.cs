using Microsoft.Win32;

namespace KanbanApp.Services;

// "Start when Windows starts": one value under the user's Run key, holding the path of this exe.
// It is a setting of this PC, not of the task file (another PC sharing the file decides for
// itself), so it lives in the registry rather than the Settings table, and the Settings screen
// reads it back from there each time. The uninstaller removes the value (installer\KanbanTaskBoard.iss).
public static class WindowsStartup
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string ValueName => AppChannel.IsTest ? "KanbanTaskBoard-Test" : "KanbanTaskBoard";

    // Swapped for a dictionary by the tests, which must never touch the real Run key.
    internal static Func<string?> Read = () => (Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue(ValueName)) as string;
    internal static Action<string?> Write = value =>
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (value is null) key.DeleteValue(ValueName, throwOnMissingValue: false);
        else key.SetValue(value: value, name: ValueName);
    };

    private static string ExePath => Environment.ProcessPath ?? string.Empty;

    // Enabled only if the value points at this very exe: a value left by a copy installed somewhere
    // else would start that one, so it reads as off (and ticking the box puts it right).
    public static bool IsEnabled
    {
        get
        {
            var value = Read();
            return value is not null && ExePath.Length > 0 && string.Equals(value.Trim('"'), ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Quoted, since the install folder has spaces in it.
    public static void SetEnabled(bool enabled)
    {
        if (!enabled) { Write(null); return; }
        if (ExePath.Length > 0) Write($"\"{ExePath}\"");
    }

    // For the tests: a store that is only a dictionary.
    internal static void UseInMemoryStore()
    {
        string? stored = null;
        Read = () => stored;
        Write = value => stored = value;
    }
}
