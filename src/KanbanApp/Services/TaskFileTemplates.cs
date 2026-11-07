using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Which parts of a task file go into a template made from it. The columns, priorities, custom
// filters, saved reports and board settings always go; finished, archived and deleted tasks, task
// history, attachments and the sharing records never do.
public sealed record TemplateParts(bool Projects = true, bool Goals = true, bool People = true, bool Flags = true,
    bool OpenTasks = false, bool TaskTemplates = true)
{
    public static readonly TemplateParts Default = new();

    public string Summary
    {
        get
        {
            var parts = new List<string> { "columns", "priorities", "settings" };
            if (Projects) parts.Add("projects");
            if (Goals) parts.Add("goals");
            if (People) parts.Add("people");
            if (Flags) parts.Add("flags");
            if (OpenTasks) parts.Add("open tasks");
            if (TaskTemplates) parts.Add("task templates");
            return string.Join(", ", parts);
        }
    }
}

// A template on disk, as the Templates list shows it.
public sealed record TaskFileTemplate(string Path, string Name, string? Description, DateTime Created, int OpenTaskCount)
{
    public string Summary => $"{(OpenTaskCount == 0 ? "No tasks" : $"{OpenTaskCount} task{(OpenTaskCount == 1 ? "" : "s")}")}, made {Created:MMM d, yyyy}";
}

// Task-file templates: a task file saved with only the parts chosen, kept in the Templates folder,
// from which any number of new task files can be started. A template is itself a SQLite task file,
// so the same code reads it, and it carries its name and description inside.
public static class TaskFileTemplates
{
    public const string Extension = ".template.db";
    public const string NameKey = "TemplateName";
    public const string DescriptionKey = "TemplateDescription";
    public const string CreatedKey = "TemplateCreated";
    public const string PartsKey = "TemplateParts";

    public static IReadOnlyList<TaskFileTemplate> List(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        var templates = new List<TaskFileTemplate>();
        foreach (var path in Directory.GetFiles(folder, "*" + Extension))
        {
            var info = TaskFileInspector.Inspect(path);
            if (!info.IsTaskFile) continue;
            var created = DateTime.TryParse(ReadSetting(path, CreatedKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when) ? when : File.GetLastWriteTime(path);
            templates.Add(new TaskFileTemplate(path, info.TemplateName ?? Path.GetFileName(path).Replace(Extension, ""), info.TemplateDescription, created, info.OpenTaskCount));
        }
        return templates.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static string PathFor(string folder, string name) => Path.Combine(folder, SafeFileName(name) + Extension);

    public static string SafeFileName(string name)
    {
        var safe = string.Concat(name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        return string.IsNullOrWhiteSpace(safe) ? "Template" : safe;
    }

    // Makes the template from a task file: a safe copy, then everything not chosen is taken out.
    public static TaskFileTemplate Save(string sourceDbPath, string templatePath, string name, string? description, TemplateParts parts)
    {
        if (File.Exists(templatePath)) File.Delete(templatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(templatePath)!);
        using (var source = new SqliteConnection($"Data Source={sourceDbPath};Pooling=False"))
        using (var target = new SqliteConnection($"Data Source={templatePath};Pooling=False"))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        using (var connection = new SqliteConnection($"Data Source={templatePath};Pooling=False"))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            void Run(string sql)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            // Never part of a template: what has happened, what was shared, files on this PC.
            Run("DELETE FROM CardHistory");
            Run("DELETE FROM SharedTasks");
            DeleteIfExists(connection, "CardAttachments");
            Run("DELETE FROM Cards WHERE IsDeleted = 1 OR IsArchived = 1 OR CompletedAt IS NOT NULL OR ColumnId IN (SELECT Id FROM Columns WHERE Name = 'Done')");
            if (!parts.OpenTasks) Run("DELETE FROM Cards");
            Run("UPDATE Cards SET ShareId = NULL, IsImported = 0");

            if (!parts.Projects) { Run("DELETE FROM Projects"); DeleteIfExists(connection, "ProjectMilestones"); Run("UPDATE Cards SET ProjectId = NULL"); }
            if (!parts.Goals) { Run("DELETE FROM Goals"); DeleteIfExists(connection, "GoalMilestones"); Run("UPDATE Cards SET GoalId = NULL"); }
            if (!parts.People) { Run("DELETE FROM People"); Run("UPDATE Cards SET WhoId = NULL, Who = NULL"); }
            if (!parts.Flags) Run("DELETE FROM Flags");
            if (!parts.TaskTemplates) Run("DELETE FROM TaskTemplates");

            // Rows that pointed at tasks, people or flags no longer there.
            Run("DELETE FROM SubTasks WHERE CardId NOT IN (SELECT Id FROM Cards)");
            Run("DELETE FROM CardFlags WHERE CardId NOT IN (SELECT Id FROM Cards) OR FlagId NOT IN (SELECT Id FROM Flags)");
            Run("DELETE FROM CardPeople WHERE CardId NOT IN (SELECT Id FROM Cards) OR PersonId NOT IN (SELECT Id FROM People)");
            Run("DELETE FROM CardRichNotes WHERE CardId NOT IN (SELECT Id FROM Cards)");
            if (TableExists(connection, "CardLinks")) Run("DELETE FROM CardLinks WHERE FromId NOT IN (SELECT Id FROM Cards) OR ToId NOT IN (SELECT Id FROM Cards)");

            // Settings that belong to one PC or one file, not to a template.
            Run($"DELETE FROM Settings WHERE Key = '{DatabaseService.FileIdKey}' OR Key LIKE 'AvailableUpdate:%' OR Key LIKE '%WindowPlacement%' OR Key = 'SkippedUpdateVersion' OR Key = 'LastSeenVersion'");
            Set(connection, NameKey, name.Trim());
            Set(connection, DescriptionKey, description?.Trim() ?? string.Empty);
            Set(connection, CreatedKey, DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            Set(connection, PartsKey, System.Text.Json.JsonSerializer.Serialize(parts));
            transaction.Commit();
        }
        Vacuum(templatePath);

        var info = TaskFileInspector.Inspect(templatePath);
        return new TaskFileTemplate(templatePath, name.Trim(), description?.Trim(), DateTime.Now, info.OpenTaskCount);
    }

    // The parts a template was made with, so Update can make it again the same way.
    public static TemplateParts PartsOf(string templatePath)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<TemplateParts>(ReadSetting(templatePath, PartsKey) ?? "") ?? TemplateParts.Default;
        }
        catch (System.Text.Json.JsonException)
        {
            return TemplateParts.Default;
        }
    }

    // A new task file from a template: a copy with its own identity and none of the template's labels.
    public static void CreateTaskFile(string templatePath, string newDbPath)
    {
        if (File.Exists(newDbPath)) throw new IOException("There is already a file at that location.");
        Directory.CreateDirectory(Path.GetDirectoryName(newDbPath)!);
        File.Copy(templatePath, newDbPath);
        using (var connection = new SqliteConnection($"Data Source={newDbPath};Pooling=False"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM Settings WHERE Key IN ('{NameKey}', '{DescriptionKey}', '{CreatedKey}', '{PartsKey}')";
            cmd.ExecuteNonQuery();
        }
        new DatabaseService(newDbPath).AssignNewFileId();
        SqliteConnection.ClearAllPools();
    }

    public static void Rename(string templatePath, string name, string? description)
    {
        using var connection = new SqliteConnection($"Data Source={templatePath};Pooling=False");
        connection.Open();
        Set(connection, NameKey, name.Trim());
        Set(connection, DescriptionKey, description?.Trim() ?? string.Empty);
    }

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        check.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(check.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static void DeleteIfExists(SqliteConnection connection, string table)
    {
        if (!TableExists(connection, table)) return;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {table}";
        cmd.ExecuteNonQuery();
    }

    private static void Set(SqliteConnection connection, string key, string value)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Settings (Key, Value) VALUES ($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    private static string? ReadSetting(string path, string key)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    private static void Vacuum(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "VACUUM";
        cmd.ExecuteNonQuery();
    }
}
