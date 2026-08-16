using System.IO;
using System.Text.Json;

namespace KanbanApp.Services;

public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppChannel.DataFolderName);

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static string DefaultDbPath => Path.Combine(ConfigDir, "kanban.db");

    public string DbPath { get; set; } = DefaultDbPath;

    public string? PendingCleanupPath { get; set; }

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
