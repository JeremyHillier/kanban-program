using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KanbanApp.Services;

// One task file the app knows about: where it is, and the name and note the user gave it here. The
// file's own contents are never in the config - only this registration.
public sealed class TaskFileEntry
{
    public string Path { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? LastOpened { get; set; }

    // The name the user gave it, or the file's own name without ".db".
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileNameWithoutExtension(Path) : Name.Trim();
}

// The small per-PC config file: which task file is open, every task file registered here (the
// catalogue the Task Files window shows), which one opens at startup, and where file templates are.
public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppChannel.DataFolderName);

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static string DefaultDbPath => Path.Combine(ConfigDir, "kanban.db");

    // Where task-file templates go unless the user chose another folder (TemplatesFolder).
    public static string DefaultTemplatesFolder => Path.Combine(ConfigDir, "Templates");

    // Where the database location itself is remembered - surfaced read-only by the About dialog,
    // since it lives outside the database and is otherwise invisible to the user.
    public static string SettingsFilePath => ConfigPath;

    // The task file that is open now (or was when the app last ran).
    public string DbPath { get; set; } = DefaultDbPath;

    public string? PendingCleanupPath { get; set; }

    // Every task file registered on this PC, the open one included. The order is not significant;
    // use FilesByRecency for a list.
    public List<TaskFileEntry> Files { get; set; } = [];

    // The file to open when the app starts: one of Files, or null for whichever was open last.
    public string? StartupDbPath { get; set; }

    public string? TemplatesFolder { get; set; }

    [JsonIgnore]
    public string TemplatesFolderOrDefault => string.IsNullOrWhiteSpace(TemplatesFolder) ? DefaultTemplatesFolder : TemplatesFolder;

    // Older configs kept the other files as a plain recent list; it is read once and folded into Files.
    public List<string>? RecentDbPaths { get; set; }

    public static bool ArePathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    public TaskFileEntry? Entry(string path) => Files.FirstOrDefault(f => ArePathsEqual(f.Path, path));

    public TaskFileEntry Current => Entry(DbPath) ?? Register(DbPath);

    // Most recently opened first, so the open file leads; never-opened files follow by name.
    [JsonIgnore]
    public IReadOnlyList<TaskFileEntry> FilesByRecency =>
        Files.OrderByDescending(f => f.LastOpened ?? DateTime.MinValue).ThenBy(f => f.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();

    // The file the app opens at startup: the chosen one if it is still registered, else the last open one.
    [JsonIgnore]
    public string FileToOpenAtStartup =>
        StartupDbPath is { } chosen && Entry(chosen) is { } entry ? entry.Path : DbPath;

    // Adds a file to the catalogue (or returns its entry if it is already there).
    public TaskFileEntry Register(string path, string? name = null, string? description = null)
    {
        if (Entry(path) is { } existing)
        {
            if (!string.IsNullOrWhiteSpace(name)) existing.Name = name.Trim();
            if (description is not null) existing.Description = description.Trim();
            return existing;
        }
        var entry = new TaskFileEntry { Path = path, Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(), Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim() };
        Files.Add(entry);
        return entry;
    }

    // Forgets a file here; the file itself is not touched. The open file cannot be forgotten.
    public bool Remove(string path)
    {
        if (ArePathsEqual(path, DbPath)) return false;
        var removed = Files.RemoveAll(f => ArePathsEqual(f.Path, path)) > 0;
        if (StartupDbPath is { } chosen && ArePathsEqual(chosen, path)) StartupDbPath = null;
        return removed;
    }

    // The file is at a new path now (moved by the app, or found again after the user moved it).
    public void Repoint(string oldPath, string newPath)
    {
        if (Entry(oldPath) is { } entry) entry.Path = newPath;
        else Register(newPath);
        if (ArePathsEqual(DbPath, oldPath)) DbPath = newPath;
        if (StartupDbPath is { } chosen && ArePathsEqual(chosen, oldPath)) StartupDbPath = newPath;
    }

    // Repoints DbPath at a different file - unlike moving/renaming the current file (see
    // SettingsWindow's Change Location), this never touches anything on disk: the old file is left
    // exactly where it is and stays in the catalogue for switching back later.
    public void SwitchTo(string newPath)
    {
        Register(newPath).LastOpened = DateTime.Now;
        DbPath = newPath;
    }

    public static bool ConfigFileExists() => File.Exists(ConfigPath);

    public static AppConfig Load()
    {
        AppConfig config = new();
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded is not null && !string.IsNullOrWhiteSpace(loaded.DbPath)) config = loaded;
            }
        }
        catch (Exception)
        {
            // Fall through to the default config if the file is missing, unreadable, or malformed.
        }

        config.Normalise();
        return config;
    }

    // The open file is always in the catalogue, and an older config's recent list becomes entries.
    public void Normalise()
    {
        Files.RemoveAll(f => string.IsNullOrWhiteSpace(f.Path));
        foreach (var path in RecentDbPaths ?? []) Register(path);
        RecentDbPaths = null;
        if (Entry(DbPath) is null) Register(DbPath).LastOpened ??= DateTime.Now;
        if (StartupDbPath is { } chosen && Entry(chosen) is null) StartupDbPath = null;
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        File.WriteAllText(ConfigPath, json);
    }
}
