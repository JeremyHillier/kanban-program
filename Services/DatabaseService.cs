using System.IO;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Split across multiple files by entity/concern (see DatabaseService.*.cs), same reasoning and
// pattern as ViewModels/MainViewModel.cs's split - this class had grown too large to navigate as
// a single file. This file holds the connection setup and small cross-cutting helpers shared by
// every other partial file (OpenConnection, NowStamp, LogHistory, MigrateColumn); everything else
// - Schema, Settings, Columns, Cards, SubTasks, Attachments, Flags, Projects, People, Goals -
// lives in its own partial file.
public partial class DatabaseService
{
    private readonly string _connectionString;

    public string DbPath { get; }

    public DatabaseService()
    {
        var config = AppConfig.Load();
        DbPath = config.DbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        _connectionString = $"Data Source={DbPath}";

        Initialize();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public static string GetAttachmentsDir(string dbPath) => Path.Combine(Path.GetDirectoryName(dbPath)!, "Attachments");

    private static void MigrateColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = pragmaCmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1) == columnName) return;
            }
        }

        using var alterCmd = connection.CreateCommand();
        alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alterCmd.ExecuteNonQuery();
    }

    private static string NowStamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private static void LogHistory(SqliteConnection connection, int cardId, string cardTitle, string eventType, string details)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO CardHistory (CardId, CardTitle, EventType, Details, Timestamp)
            VALUES ($cardId, $cardTitle, $eventType, $details, $timestamp);
            """;
        cmd.Parameters.AddWithValue("$cardId", cardId);
        cmd.Parameters.AddWithValue("$cardTitle", cardTitle);
        cmd.Parameters.AddWithValue("$eventType", eventType);
        cmd.Parameters.AddWithValue("$details", details);
        cmd.Parameters.AddWithValue("$timestamp", NowStamp());
        cmd.ExecuteNonQuery();
    }
}
