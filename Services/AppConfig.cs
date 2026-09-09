using System.IO;
using System.Linq;
using System.Text.Json;

namespace KanbanApp.Services;

public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppChannel.DataFolderName);

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static string DefaultDbPath => Path.Combine(ConfigDir, "kanban.db");

    // Where the database location itself is remembered - surfaced read-only by the About dialog,
    // since it lives outside the database and is otherwise invisible to the user.
    public static string SettingsFilePath => ConfigPath;

    public string DbPath { get; set; } = DefaultDbPath;

    public string? PendingCleanupPath { get; set; }

    // Other task files the user has switched away from (most-recently-used first) - lets Settings
    // offer a quick-switch list, similar to recent company files in accounting software. Capped so
    // it doesn't grow unbounded across years of switching.
    public List<string> RecentDbPaths { get; set; } = [];

    private const int MaxRecentDbPaths = 8;

    public static bool ArePathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    // Repoints DbPath at a different file - unlike moving/renaming the current file (see
    // SettingsWindow's Change Location), this never touches anything on disk: the old file is left
    // exactly where it is and just gets remembered in RecentDbPaths for switching back later.
    public void SwitchTo(string newPath)
    {
        if (!string.IsNullOrWhiteSpace(DbPath) && !ArePathsEqual(DbPath, newPath))
        {
            RecentDbPaths.RemoveAll(p => ArePathsEqual(p, DbPath));
            RecentDbPaths.Insert(0, DbPath);
        }

        RecentDbPaths.RemoveAll(p => ArePathsEqual(p, newPath));
        if (RecentDbPaths.Count > MaxRecentDbPaths)
        {
            RecentDbPaths = RecentDbPaths.Take(MaxRecentDbPaths).ToList();
        }

        DbPath = newPath;
    }

    public static bool ConfigFileExists() => File.Exists(ConfigPath);

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config is not null && !string.IsNullOrWhiteSpace(config.DbPath))
                {
                    return config;
                }
            }
        }
        catch (Exception)
        {
            // Fall through to default config below if the file is missing, unreadable, or malformed.
        }

        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
