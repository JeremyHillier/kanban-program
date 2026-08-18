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
                CREATE TABLE IF NOT EXISTS SubTasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    IsDone INTEGER NOT NULL DEFAULT 0,
                    SortOrder INTEGER NOT NULL
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
                CREATE TABLE IF NOT EXISTS CardAttachments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardId INTEGER NOT NULL,
                    FilePath TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    AddedDate TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS People (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );
                """;
            cmd.ExecuteNonQuery();
        }

        MigrateColumn(connection, "Cards", "ProjectId", "INTEGER NULL");
        MigrateColumn(connection, "Cards", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
        MigrateColumn(connection, "Cards", "Priority", "TEXT NOT NULL DEFAULT 'Normal'");
        MigrateColumn(connection, "Cards", "DueDate", "TEXT NULL");
        MigrateColumn(connection, "Cards", "Who", "TEXT NULL");
        MigrateColumn(connection, "Cards", "LastUpdated", "TEXT NULL");
        MigrateColumn(connection, "Cards", "IsRecurring", "INTEGER NOT NULL DEFAULT 0");
        MigrateColumn(connection, "Cards", "RecurrencePattern", "TEXT NULL");
        MigrateColumn(connection, "Cards", "GoalId", "INTEGER NULL");
        MigrateColumn(connection, "Cards", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
        MigrateColumn(connection, "Cards", "Notes", "TEXT NULL");
        MigrateColumn(connection, "Cards", "IsImported", "INTEGER NOT NULL DEFAULT 0");
        MigrateColumn(connection, "Cards", "WhoId", "INTEGER NULL");
        MigrateColumn(connection, "Cards", "ForceEditOnComplete", "INTEGER NOT NULL DEFAULT 0");
        MigrateColumn(connection, "Projects", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        MigrateColumn(connection, "Goals", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        MigrateColumn(connection, "Flags", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        MigrateColumn(connection, "Columns", "DisplayName", "TEXT");

        using (var backfillCmd = connection.CreateCommand())
        {
            backfillCmd.CommandText = "UPDATE Columns SET DisplayName = Name WHERE DisplayName IS NULL;";
            backfillCmd.ExecuteNonQuery();
        }

        BackfillPeopleFromLegacyWho(connection);

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

    private void BackfillPeopleFromLegacyWho(SqliteConnection connection)
    {
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM People;";
            var count = (long)checkCmd.ExecuteScalar()!;
            if (count > 0) return; // Already migrated, or people have already been added through the app.
        }

        var distinctNames = new List<string>();
        using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.CommandText = "SELECT DISTINCT Who FROM Cards WHERE Who IS NOT NULL AND TRIM(Who) != '' ORDER BY Who;";
            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                distinctNames.Add(reader.GetString(0).Trim());
            }
        }

        foreach (var name in distinctNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var person = AddPerson(name, connection);

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE Cards SET WhoId = $whoId WHERE Who = $who COLLATE NOCASE;";
            updateCmd.Parameters.AddWithValue("$whoId", person.Id);
            updateCmd.Parameters.AddWithValue("$who", name);
            updateCmd.ExecuteNonQuery();
        }
    }

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
        cmd.CommandText = "SELECT Id, Name, DisplayName, SortOrder FROM Columns ORDER BY SortOrder;";

        var result = new List<KanbanColumn>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            result.Add(new KanbanColumn
            {
                Id = reader.GetInt32(0),
                Name = name,
                DisplayName = reader.IsDBNull(2) ? name : reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return result;
    }

    public void RenameColumnDisplayName(int columnId, string displayName)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Columns SET DisplayName = $displayName WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$displayName", displayName);
        cmd.Parameters.AddWithValue("$id", columnId);
        cmd.ExecuteNonQuery();
    }

    public List<CardItem> GetCards(bool archivedOnly = false)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        var archivedAtColumn = archivedOnly
            ? ", (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = Cards.Id AND h.EventType = 'Archived')"
            : "";
        cmd.CommandText = $"""
            SELECT Id, ColumnId, Title, SortOrder, ProjectId, Priority, DueDate, WhoId, LastUpdated, IsRecurring, RecurrencePattern, GoalId, Notes, IsImported, ForceEditOnComplete{archivedAtColumn}
            FROM Cards WHERE IsArchived = {(archivedOnly ? 1 : 0)} AND IsDeleted = 0 ORDER BY SortOrder;
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
                    WhoId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    LastUpdated = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                    IsRecurring = reader.GetInt32(9) != 0,
                    RecurrencePattern = reader.IsDBNull(10) ? null : reader.GetString(10),
                    GoalId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    Notes = reader.IsDBNull(12) ? null : reader.GetString(12),
                    IsImported = reader.GetInt32(13) != 0,
                    ForceEditOnComplete = reader.GetInt32(14) != 0,
                    ArchivedAt = archivedOnly && !reader.IsDBNull(15) ? DateTime.Parse(reader.GetString(15)) : null
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

        using (var subCmd = connection.CreateCommand())
        {
            subCmd.CommandText = "SELECT Id, CardId, Title, IsDone, SortOrder FROM SubTasks ORDER BY CardId, SortOrder;";
            using var reader = subCmd.ExecuteReader();
            var byCard = result.ToDictionary(c => c.Id);
            while (reader.Read())
            {
                var cardId = reader.GetInt32(1);
                if (byCard.TryGetValue(cardId, out var card))
                {
                    card.SubTasks.Add(new SubTaskItem
                    {
                        Id = reader.GetInt32(0),
                        CardId = cardId,
                        Title = reader.GetString(2),
                        IsDone = reader.GetInt32(3) != 0,
                        SortOrder = reader.GetInt32(4)
                    });
                }
            }
        }

        using (var attCmd = connection.CreateCommand())
        {
            attCmd.CommandText = "SELECT Id, CardId, FilePath, DisplayName, AddedDate FROM CardAttachments ORDER BY CardId, Id;";
            using var reader = attCmd.ExecuteReader();
            var byCard = result.ToDictionary(c => c.Id);
            while (reader.Read())
            {
                var cardId = reader.GetInt32(1);
                if (byCard.TryGetValue(cardId, out var card))
                {
                    card.Attachments.Add(new CardAttachment
                    {
                        Id = reader.GetInt32(0),
                        CardId = cardId,
                        FilePath = reader.GetString(2),
                        DisplayName = reader.GetString(3),
                        AddedDate = DateTime.Parse(reader.GetString(4))
                    });
                }
            }
        }

        return result;
    }

    public List<Project> GetProjects()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM Projects ORDER BY Name COLLATE NOCASE;";

        var result = new List<Project>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Project
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
            });
        }
        return result;
    }

    public List<Person> GetPeople()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM People ORDER BY Name COLLATE NOCASE;";

        var result = new List<Person>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Person
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
            });
        }
        return result;
    }

    public List<Goal> GetGoals()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM Goals ORDER BY Name COLLATE NOCASE;";

        var result = new List<Goal>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Goal
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
            });
        }
        return result;
    }

    public List<Flag> GetFlags()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM Flags ORDER BY Name COLLATE NOCASE;";

        var result = new List<Flag>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Flag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) != 0
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

    public void SetFlagActive(int flagId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Flags SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
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

    public List<SubTaskItem> SetCardSubTasks(int cardId, List<(string Title, bool IsDone)> subTasks)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "DELETE FROM SubTasks WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        var result = new List<SubTaskItem>();
        var sortOrder = 0;
        foreach (var (title, isDone) in subTasks)
        {
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO SubTasks (CardId, Title, IsDone, SortOrder) VALUES ($cardId, $title, $isDone, $sortOrder);
                SELECT last_insert_rowid();
                """;
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            insertCmd.Parameters.AddWithValue("$title", title);
            insertCmd.Parameters.AddWithValue("$isDone", isDone ? 1 : 0);
            insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
            var id = (long)insertCmd.ExecuteScalar()!;

            result.Add(new SubTaskItem { Id = (int)id, CardId = cardId, Title = title, IsDone = isDone, SortOrder = sortOrder });
            sortOrder++;
        }

        return result;
    }

    public void SetSubTaskDone(int subTaskId, bool isDone)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE SubTasks SET IsDone = $isDone WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isDone", isDone ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", subTaskId);
        cmd.ExecuteNonQuery();
    }

    public static string GetAttachmentsDir(string dbPath) => Path.Combine(Path.GetDirectoryName(dbPath)!, "Attachments");

    public List<CardAttachment> SetCardAttachments(int cardId, List<(string FilePath, string DisplayName, DateTime AddedDate)> attachments)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "DELETE FROM CardAttachments WHERE CardId = $cardId;";
            clearCmd.Parameters.AddWithValue("$cardId", cardId);
            clearCmd.ExecuteNonQuery();
        }

        var result = new List<CardAttachment>();
        foreach (var (filePath, displayName, addedDate) in attachments)
        {
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO CardAttachments (CardId, FilePath, DisplayName, AddedDate) VALUES ($cardId, $filePath, $displayName, $addedDate);
                SELECT last_insert_rowid();
                """;
            insertCmd.Parameters.AddWithValue("$cardId", cardId);
            insertCmd.Parameters.AddWithValue("$filePath", filePath);
            insertCmd.Parameters.AddWithValue("$displayName", displayName);
            insertCmd.Parameters.AddWithValue("$addedDate", addedDate.ToString("yyyy-MM-dd HH:mm:ss"));
            var id = (long)insertCmd.ExecuteScalar()!;

            result.Add(new CardAttachment { Id = (int)id, CardId = cardId, FilePath = filePath, DisplayName = displayName, AddedDate = addedDate });
        }

        return result;
    }

    public bool IsAttachmentPathReferencedElsewhere(string filePath, int excludingCardId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM CardAttachments WHERE FilePath = $filePath AND CardId != $cardId;";
        cmd.Parameters.AddWithValue("$filePath", filePath);
        cmd.Parameters.AddWithValue("$cardId", excludingCardId);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    private KanbanColumn AddColumn(string name, SqliteConnection connection)
    {
        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Columns;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Columns (Name, DisplayName, SortOrder) VALUES ($name, $name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new KanbanColumn { Id = (int)id, Name = name, DisplayName = name, SortOrder = (int)sortOrder };
    }

    public CardItem AddCard(int columnId, string title, int? projectId, string columnName, string priority, DateTime? dueDate, int? whoId,
        bool isRecurring, string? recurrencePattern, int? goalId, string? notes = null, bool isImported = false, bool forceEditOnComplete = false)
    {
        using var connection = OpenConnection();

        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Cards WHERE ColumnId = $columnId;";
        maxCmd.Parameters.AddWithValue("$columnId", columnId);
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        var now = NowStamp();
        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO Cards (ColumnId, Title, SortOrder, ProjectId, Priority, DueDate, WhoId, LastUpdated, IsRecurring, RecurrencePattern, GoalId, Notes, IsImported, ForceEditOnComplete)
            VALUES ($columnId, $title, $sortOrder, $projectId, $priority, $dueDate, $whoId, $lastUpdated, $isRecurring, $recurrencePattern, $goalId, $notes, $isImported, $forceEditOnComplete);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$columnId", columnId);
        insertCmd.Parameters.AddWithValue("$title", title);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        insertCmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$priority", priority);
        insertCmd.Parameters.AddWithValue("$dueDate", (object?)dueDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$whoId", (object?)whoId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$lastUpdated", now);
        insertCmd.Parameters.AddWithValue("$isRecurring", isRecurring ? 1 : 0);
        insertCmd.Parameters.AddWithValue("$recurrencePattern", (object?)recurrencePattern ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$goalId", (object?)goalId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$isImported", isImported ? 1 : 0);
        insertCmd.Parameters.AddWithValue("$forceEditOnComplete", forceEditOnComplete ? 1 : 0);
        var id = (long)insertCmd.ExecuteScalar()!;

        LogHistory(connection, (int)id, title, "Created", $"Added to {columnName}");

        return new CardItem
        {
            Id = (int)id, ColumnId = columnId, Title = title, SortOrder = (int)sortOrder, ProjectId = projectId,
            Priority = priority, DueDate = dueDate, WhoId = whoId, LastUpdated = DateTime.Parse(now),
            IsRecurring = isRecurring, RecurrencePattern = recurrencePattern, GoalId = goalId, Notes = notes, IsImported = isImported,
            ForceEditOnComplete = forceEditOnComplete
        };
    }

    public void SetCardImported(int cardId, bool isImported)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Cards SET IsImported = $isImported WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isImported", isImported ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", cardId);
        cmd.ExecuteNonQuery();
    }

    public DateTime UpdateCard(int cardId, string title, int? projectId, string priority, DateTime? dueDate, int? whoId,
        bool isRecurring, string? recurrencePattern, int? goalId, string? notes = null, bool forceEditOnComplete = false)
    {
        using var connection = OpenConnection();
        var now = NowStamp();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE Cards SET Title = $title, ProjectId = $projectId, Priority = $priority,
                    DueDate = $dueDate, WhoId = $whoId, LastUpdated = $lastUpdated,
                    IsRecurring = $isRecurring, RecurrencePattern = $recurrencePattern, GoalId = $goalId, Notes = $notes,
                    ForceEditOnComplete = $forceEditOnComplete
                WHERE Id = $id;
                """;
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$priority", priority);
            cmd.Parameters.AddWithValue("$dueDate", (object?)dueDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$whoId", (object?)whoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lastUpdated", now);
            cmd.Parameters.AddWithValue("$isRecurring", isRecurring ? 1 : 0);
            cmd.Parameters.AddWithValue("$recurrencePattern", (object?)recurrencePattern ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$goalId", (object?)goalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$forceEditOnComplete", forceEditOnComplete ? 1 : 0);
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

    public void SetProjectActive(int projectId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Projects SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
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

    public Person AddPerson(string name)
    {
        using var connection = OpenConnection();
        return AddPerson(name, connection);
    }

    private Person AddPerson(string name, SqliteConnection connection)
    {
        using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM People;";
        var sortOrder = (long)maxCmd.ExecuteScalar()!;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO People (Name, SortOrder) VALUES ($name, $sortOrder);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        var id = (long)insertCmd.ExecuteScalar()!;

        return new Person { Id = (int)id, Name = name, SortOrder = (int)sortOrder };
    }

    public void RenamePerson(int personId, string name)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE People SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", personId);
        cmd.ExecuteNonQuery();
    }

    public void SetPersonActive(int personId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE People SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", personId);
        cmd.ExecuteNonQuery();
    }

    public void DeletePerson(int personId)
    {
        using var connection = OpenConnection();

        using (var clearCmd = connection.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Cards SET WhoId = NULL WHERE WhoId = $id;";
            clearCmd.Parameters.AddWithValue("$id", personId);
            clearCmd.ExecuteNonQuery();
        }

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM People WHERE Id = $id;";
        deleteCmd.Parameters.AddWithValue("$id", personId);
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

    public void SetGoalActive(int goalId, bool isActive)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Goals SET IsActive = $isActive WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
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
