using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// What a task file is, read without opening it as the app would (which would bring it up to date
// and stamp it): whether it is a task file at all, its format against this app's, its identity and
// how many tasks it holds. Used before a file is registered, switched to or made a template of.
public sealed record TaskFileInfo(bool IsTaskFile, string? Problem, int FileFormat, string? FileId, int OpenTaskCount,
    string? TemplateName, string? TemplateDescription)
{
    public bool IsFromNewerApp => FileFormat > DatabaseService.CurrentFileFormat;
    public bool IsTemplate => TemplateName is not null;

    public static TaskFileInfo NotATaskFile(string problem) => new(false, problem, 0, null, 0, null, null);
}

public static class TaskFileInspector
{
    public static TaskFileInfo Inspect(string path)
    {
        if (!File.Exists(path)) return TaskFileInfo.NotATaskFile("There is no file at this location.");

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); // the app may have it open
            var header = new byte[16];
            var read = stream.Read(header, 0, header.Length);
            if (read < 16 || System.Text.Encoding.ASCII.GetString(header, 0, 15) != "SQLite format 3")
                return TaskFileInfo.NotATaskFile("This is not a task file: it is not a SQLite database.");
        }
        catch (IOException ex)
        {
            return TaskFileInfo.NotATaskFile($"The file could not be read.\n\n{ex.Message}");
        }

        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();

            if (!HasTable(connection, "Cards") || !HasTable(connection, "Settings") || !HasTable(connection, "Columns"))
                return TaskFileInfo.NotATaskFile("This is not a task file: it has no tasks table.");

            var format = int.TryParse(Setting(connection, "FileFormat"), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
            var openTasks = Count(connection, "SELECT COUNT(*) FROM Cards WHERE IsDeleted = 0 AND IsArchived = 0");
            return new TaskFileInfo(true, null, format, Setting(connection, DatabaseService.FileIdKey), openTasks,
                Setting(connection, TaskFileTemplates.NameKey), Setting(connection, TaskFileTemplates.DescriptionKey));
        }
        catch (SqliteException ex)
        {
            return TaskFileInfo.NotATaskFile($"The file could not be opened as a task file.\n\n{ex.Message}");
        }
    }

    private static bool HasTable(SqliteConnection connection, string name)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        cmd.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static string? Setting(SqliteConnection connection, string key)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    private static int Count(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
