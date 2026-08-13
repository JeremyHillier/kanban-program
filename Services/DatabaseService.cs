using System.IO;
using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KanbanApp");
        Directory.CreateDirectory(appDataDir);
        var dbPath = Path.Combine(appDataDir, "kanban.db");
        _connectionString = $"Data Source={dbPath}";

        Initialize();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Columns (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Cards (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ColumnId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    FOREIGN KEY (ColumnId) REFERENCES Columns(Id) ON DELETE CASCADE
                );
                """;
            cmd.ExecuteNonQuery();
        }

        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM Columns;";
            var count = (long)checkCmd.ExecuteScalar()!;
            if (count == 0)
            {
                foreach (var name in new[] { "To Do", "In Progress", "On Hold", "Waiting", "Done" })
                {
                    AddColumn(name, connection);
                }
            }
        }
    }

    public List<KanbanColumn> GetColumns()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder FROM Columns ORDER BY SortOrder;";

        var result = new List<KanbanColumn>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new KanbanColumn
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2)
            });
        }
        return result;
    }

    public List<CardItem> GetCards()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, ColumnId, Title, SortOrder FROM Cards ORDER BY SortOrder;";

        var result = new List<CardItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new CardItem
            {
                Id = reader.GetInt32(0),
                ColumnId = reader.GetInt32(1),
                Title = reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return result;
    }

    private KanbanColumn AddColumn(string name, SqliteConnection connection)
    {
        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Columns;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Columns (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new KanbanColumn { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public CardItem AddCard(int columnId, string title)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", columnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Cards (ColumnId, Title, SortOrder) VALUES ($columnId, $title, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$columnId", columnId);
        insertCmd.Parameters.AddWithValue("$title", title);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new CardItem { Id = (int)id, ColumnId = columnId, Title = title, SortOrder = (int)sortOrder };
    }

    public void MoveCard(int cardId, int newColumnId)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", newColumnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE Cards SET ColumnId = $columnId, SortOrder = $sortOrder WHERE Id = $id;";
        updateCmd.Parameters.AddWithValue("$columnId", newColumnId);
        updateCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        updateCmd.Parameters.AddWithValue("$id", cardId);
        updateCmd.ExecuteNonQuery();
    }

    public void DeleteCard(int cardId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Cards WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", cardId);
        cmd.ExecuteNonQuery();
    }
}
