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
                CREATE TABLE IF NOT EXISTS Projects (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Cards (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ColumnId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    ProjectId INTEGER NULL,
                    IsArchived INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (ColumnId) REFERENCES Columns(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL
                );
                CREATE TABLE IF NOT EXISTS CardHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardId INTEGER NOT NULL,
                    CardTitle TEXT NOT NULL,
                    EventType TEXT NOT NULL,
                    Details TEXT NOT NULL,
                    Timestamp TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        MigrateCardsColumn(connection, "ProjectId", "INTEGER NULL");
        MigrateCardsColumn(connection, "IsArchived", "INTEGER NOT NULL DEFAULT 0");

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

        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM Projects;";
            var count = (long)checkCmd.ExecuteScalar()!;
            if (count == 0)
            {
                AddProject("General", connection);
            }
        }
    }

    private static void MigrateCardsColumn(SqliteConnection connection, string columnName, string columnDefinition)
    {
        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA table_info(Cards);";
            using var reader = pragmaCmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1) == columnName) return;
            }
        }

        using var alterCmd = connection.CreateCommand();
        alterCmd.CommandText = $"ALTER TABLE Cards ADD COLUMN {columnName} {columnDefinition};";
        alterCmd.ExecuteNonQuery();
    }

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
        cmd.Parameters.AddWithValue("$timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
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
        cmd.CommandText = "SELECT Id, ColumnId, Title, SortOrder, ProjectId FROM Cards WHERE IsArchived = 0 ORDER BY SortOrder;";

        var result = new List<CardItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new CardItem
            {
                Id = reader.GetInt32(0),
                ColumnId = reader.GetInt32(1),
                Title = reader.GetString(2),
                SortOrder = reader.GetInt32(3),
                ProjectId = reader.IsDBNull(4) ? null : reader.GetInt32(4)
            });
        }
        return result;
    }

    public List<Project> GetProjects()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder FROM Projects ORDER BY SortOrder;";

        var result = new List<Project>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Project
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2)
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

    public CardItem AddCard(int columnId, string title, int? projectId, string columnName)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", columnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Cards (ColumnId, Title, SortOrder, ProjectId) VALUES ($columnId, $title, $sortOrder, $projectId);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$columnId", columnId);
        insertCmd.Parameters.AddWithValue("$title", title);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        insertCmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
        var id = (long)insertCmd.ExecuteScalar()!;

        LogHistory(connection, (int)id, title, "Created", $"Added to {columnName}");

        return new CardItem { Id = (int)id, ColumnId = columnId, Title = title, SortOrder = (int)sortOrder, ProjectId = projectId };
    }

    public Project AddProject(string name)
    {
        using var connection = OpenConnection();
        return AddProject(name, connection);
    }

    private Project AddProject(string name, SqliteConnection connection)
    {
        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Projects;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Projects (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Project { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenameProject(int projectId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Projects SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", projectId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteProject(int projectId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Cards SET ProjectId = NULL WHERE ProjectId = $id;";
            clearCmd.Parameters.AddWithValue("$id", projectId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Projects WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", projectId);
        deleteCmd.ExecuteNonQuery();
    }

    public void MoveCard(int cardId, int newColumnId, string cardTitle, string fromColumnName, string toColumnName)
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

        LogHistory(connection, cardId, cardTitle, "Moved", $"Moved from {fromColumnName} to {toColumnName}");
    }

    public void DeleteCard(int cardId, string cardTitle, string columnName)
    {
        using var connection = OpenConnection();

        LogHistory(connection, cardId, cardTitle, "Deleted", $"Deleted from {columnName}");

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Cards WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", cardId);
        cmd.ExecuteNonQuery();
    }

    public void ArchiveCard(int cardId, string cardTitle, string columnName)
    {
        using var connection = OpenConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Cards SET IsArchived = 1 WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, cardTitle, "Archived", $"Archived from {columnName}");
    }

    public List<ArchivedCardInfo> GetArchivedCards()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT c.Title, col.Name, COALESCE(p.Name, 'No Project'),
                (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = c.Id AND h.EventType = 'Archived')
            FROM Cards c
            JOIN Columns col ON col.Id = c.ColumnId
            LEFT JOIN Projects p ON p.Id = c.ProjectId
            WHERE c.IsArchived = 1
            ORDER BY c.Id DESC;
            """;

        var result = new List<ArchivedCardInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ArchivedCardInfo
            {
                Title = reader.GetString(0),
                ColumnName = reader.GetString(1),
                ProjectName = reader.GetString(2),
                ArchivedAt = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3)
            });
        }
        return result;
    }
}
