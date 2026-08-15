using System.IO;
using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

public class DatabaseService
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
                CREATE TABLE IF NOT EXISTS Goals (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Flags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS CardFlags (
                    CardId INTEGER NOT NULL,
                    FlagId INTEGER NOT NULL,
                    PRIMARY KEY (CardId, FlagId)
                );
                CREATE TABLE IF NOT EXISTS Cards (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ColumnId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    ProjectId INTEGER NULL,
                    IsArchived INTEGER NOT NULL DEFAULT 0,
                    Priority TEXT NOT NULL DEFAULT 'Normal',
                    DueDate TEXT NULL,
                    Who TEXT NULL,
                    LastUpdated TEXT NULL,
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
                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        MigrateCardsColumn(connection, "ProjectId", "INTEGER NULL");
        MigrateCardsColumn(connection, "IsArchived", "INTEGER NOT NULL DEFAULT 0");
        MigrateCardsColumn(connection, "Priority", "TEXT NOT NULL DEFAULT 'Normal'");
        MigrateCardsColumn(connection, "DueDate", "TEXT NULL");
        MigrateCardsColumn(connection, "Who", "TEXT NULL");
        MigrateCardsColumn(connection, "LastUpdated", "TEXT NULL");
        MigrateCardsColumn(connection, "IsRecurring", "INTEGER NOT NULL DEFAULT 0");
        MigrateCardsColumn(connection, "RecurrencePattern", "TEXT NULL");
        MigrateCardsColumn(connection, "GoalId", "INTEGER NULL");
        MigrateCardsColumn(connection, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");

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

    public string? GetSetting(string key)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = $value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
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
        cmd.CommandText = """
            SELECT Id, ColumnId, Title, SortOrder, ProjectId, Priority, DueDate, Who, LastUpdated, IsRecurring, RecurrencePattern, GoalId
            FROM Cards WHERE IsArchived = 0 AND IsDeleted = 0 ORDER BY SortOrder;
            """;

        var result = new List<CardItem>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Add(new CardItem
                {
                    Id = reader.GetInt32(0),
                    ColumnId = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    SortOrder = reader.GetInt32(3),
                    ProjectId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Priority = reader.GetString(5),
                    DueDate = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                    Who = reader.IsDBNull(7) ? null : reader.GetString(7),
                    LastUpdated = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                    IsRecurring = reader.GetInt32(9) != 0,
                    RecurrencePattern = reader.IsDBNull(10) ? null : reader.GetString(10),
                    GoalId = reader.IsDBNull(11) ? null : reader.GetInt32(11)
                });
            }
        }

        using (var flagCmd = connection.CreateCommand())
        {
            flagCmd.CommandText = "SELECT CardId, FlagId FROM CardFlags;";
            using var reader = flagCmd.ExecuteReader();
            var byCard = result.ToDictionary(c => c.Id);
            while (reader.Read())
            {
                var cardId = reader.GetInt32(0);
                if (byCard.TryGetValue(cardId, out var card))
                {
                    card.FlagIds.Add(reader.GetInt32(1));
                }
            }
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

    public List<Goal> GetGoals()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder FROM Goals ORDER BY SortOrder;";

        var result = new List<Goal>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Goal
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2)
            });
        }
        return result;
    }

    public List<Flag> GetFlags()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder FROM Flags ORDER BY SortOrder;";

        var result = new List<Flag>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Flag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2)
            });
        }
        return result;
    }

    public Flag AddFlag(string name)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Flags;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Flags (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Flag { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenameFlag(int flagId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Flags SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", flagId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteFlag(int flagId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "DELETE FROM CardFlags WHERE FlagId = $id;";
            clearCmd.Parameters.AddWithValue("$id", flagId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Flags WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", flagId);
        deleteCmd.ExecuteNonQuery();
    }

    public void SetCardFlags(int cardId, IEnumerable<int> flagIds)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "DELETE FROM CardFlags WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        foreach (var flagId in flagIds)
        {
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO CardFlags (CardId, FlagId) VALUES ($cardId, $flagId);";
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            insertCmd.Parameters.AddWithValue("$flagId", flagId);
            insertCmd.ExecuteNonQuery();
        }
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

    public CardItem AddCard(int columnId, string title, int? projectId, string columnName, string priority, DateTime? dueDate, string? who,
        bool isRecurring, string? recurrencePattern, int? goalId)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", columnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        var now = NowStamp();
        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Cards (ColumnId, Title, SortOrder, ProjectId, Priority, DueDate, Who, LastUpdated, IsRecurring, RecurrencePattern, GoalId)
            VALUES ($columnId, $title, $sortOrder, $projectId, $priority, $dueDate, $who, $lastUpdated, $isRecurring, $recurrencePattern, $goalId);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$columnId", columnId);
        insertCmd.Parameters.AddWithValue("$title", title);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        insertCmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$priority", priority);
        insertCmd.Parameters.AddWithValue("$dueDate", (object?)dueDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$who", (object?)who ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$lastUpdated", now);
        insertCmd.Parameters.AddWithValue("$isRecurring", isRecurring ? 1 : 0);
        insertCmd.Parameters.AddWithValue("$recurrencePattern", (object?)recurrencePattern ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$goalId", (object?)goalId ?? DBNull.Value);
        var id = (long)insertCmd.ExecuteScalar()!;

        LogHistory(connection, (int)id, title, "Created", $"Added to {columnName}");

        return new CardItem
        {
            Id = (int)id, ColumnId = columnId, Title = title, SortOrder = (int)sortOrder, ProjectId = projectId,
            Priority = priority, DueDate = dueDate, Who = who, LastUpdated = DateTime.Parse(now),
            IsRecurring = isRecurring, RecurrencePattern = recurrencePattern, GoalId = goalId
        };
    }

    public DateTime UpdateCard(int cardId, string title, int? projectId, string priority, DateTime? dueDate, string? who,
        bool isRecurring, string? recurrencePattern, int? goalId)
    {
        using var connection = OpenConnection();
        var now = NowStamp();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE Cards SET Title = $title, ProjectId = $projectId, Priority = $priority,
                    DueDate = $dueDate, Who = $who, LastUpdated = $lastUpdated,
                    IsRecurring = $isRecurring, RecurrencePattern = $recurrencePattern, GoalId = $goalId
                WHERE Id = $id;
                """;
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$priority", priority);
            cmd.Parameters.AddWithValue("$dueDate", (object?)dueDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$who", (object?)who ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lastUpdated", now);
            cmd.Parameters.AddWithValue("$isRecurring", isRecurring ? 1 : 0);
            cmd.Parameters.AddWithValue("$recurrencePattern", (object?)recurrencePattern ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$goalId", (object?)goalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, title, "Edited", "Task details updated");
        return DateTime.Parse(now);
    }

    public void UpdateSortOrders(IEnumerable<(int CardId, int SortOrder)> updates)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "UPDATE Cards SET SortOrder = $sortOrder WHERE Id = $id;";
        var sortOrderParam = cmd.CreateParameter();
        sortOrderParam.ParameterName = "$sortOrder";
        cmd.Parameters.Add(sortOrderParam);
        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "$id";
        cmd.Parameters.Add(idParam);

        foreach (var (cardId, sortOrder) in updates)
        {
            sortOrderParam.Value = sortOrder;
            idParam.Value = cardId;
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
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

    public Goal AddGoal(string name)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Goals;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Goals (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Goal { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenameGoal(int goalId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Goals SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", goalId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGoal(int goalId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Cards SET GoalId = NULL WHERE GoalId = $id;";
            clearCmd.Parameters.AddWithValue("$id", goalId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Goals WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", goalId);
        deleteCmd.ExecuteNonQuery();
    }

    public DateTime MoveCard(int cardId, int newColumnId, string cardTitle, string fromColumnName, string toColumnName)
    {
        using var connection = OpenConnection();
        var now = NowStamp();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", newColumnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE Cards SET ColumnId = $columnId, SortOrder = $sortOrder, LastUpdated = $lastUpdated WHERE Id = $id;";
        updateCmd.Parameters.AddWithValue("$columnId", newColumnId);
        updateCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        updateCmd.Parameters.AddWithValue("$lastUpdated", now);
        updateCmd.Parameters.AddWithValue("$id", cardId);
        updateCmd.ExecuteNonQuery();

        LogHistory(connection, cardId, cardTitle, "Moved", $"Moved from {fromColumnName} to {toColumnName}");
        return DateTime.Parse(now);
    }

    public void DeleteCard(int cardId, string cardTitle, string columnName)
    {
        using var connection = OpenConnection();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Cards SET IsDeleted = 1, LastUpdated = $lastUpdated WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$lastUpdated", NowStamp());
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, cardTitle, "Deleted", $"Deleted from {columnName}");
    }

    public DateTime ReactivateCard(int cardId, string cardTitle)
    {
        using var connection = OpenConnection();
        var now = NowStamp();

        using var toDoCmd = connection.CreateCommand();
        toDoCmd.CommandText = "SELECT Id FROM Columns WHERE Name = 'To Do' LIMIT 1;";
        var toDoColumnId = (long)toDoCmd.ExecuteScalar()!;

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", toDoColumnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE Cards SET IsArchived = 0, IsDeleted = 0, ColumnId = $columnId, SortOrder = $sortOrder, LastUpdated = $lastUpdated
                WHERE Id = $id;
                """;
            cmd.Parameters.AddWithValue("$columnId", toDoColumnId);
            cmd.Parameters.AddWithValue("$sortOrder", sortOrder);
            cmd.Parameters.AddWithValue("$lastUpdated", now);
            cmd.Parameters.AddWithValue("$id", cardId);
            cmd.ExecuteNonQuery();
        }

        LogHistory(connection, cardId, cardTitle, "Reactivated", "Moved to To Do");
        return DateTime.Parse(now);
    }

    public void ArchiveCard(int cardId, string cardTitle, string columnName)
    {
        using var connection = OpenConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Cards SET IsArchived = 1, LastUpdated = $lastUpdated WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$lastUpdated", NowStamp());
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
            SELECT c.Id, c.Title, col.Name, COALESCE(p.Name, 'No Project'),
                (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = c.Id AND h.EventType = 'Archived')
            FROM Cards c
            JOIN Columns col ON col.Id = c.ColumnId
            LEFT JOIN Projects p ON p.Id = c.ProjectId
            WHERE c.IsArchived = 1 AND c.IsDeleted = 0
            ORDER BY c.Id DESC;
            """;

        var result = new List<ArchivedCardInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ArchivedCardInfo
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                ColumnName = reader.GetString(2),
                ProjectName = reader.GetString(3),
                ArchivedAt = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4)
            });
        }
        return result;
    }

    public List<DeletedCardInfo> GetDeletedCards()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT c.Id, c.Title, col.Name, COALESCE(p.Name, 'No Project'),
                (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = c.Id AND h.EventType = 'Deleted')
            FROM Cards c
            JOIN Columns col ON col.Id = c.ColumnId
            LEFT JOIN Projects p ON p.Id = c.ProjectId
            WHERE c.IsDeleted = 1
            ORDER BY c.Id DESC;
            """;

        var result = new List<DeletedCardInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new DeletedCardInfo
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                ColumnName = reader.GetString(2),
                ProjectName = reader.GetString(3),
                DeletedAt = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4)
            });
        }
        return result;
    }
}
