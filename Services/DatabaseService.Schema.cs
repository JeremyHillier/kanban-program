using KanbanApp.Models;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Table creation, column migrations, one-time data backfills, and default-row seeding - run once
// per DatabaseService construction (i.e. once per app startup) via Initialize().
public partial class DatabaseService
{
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
        MigrateColumn(connection, "Cards", "NextOccurrenceSpawned", "INTEGER NOT NULL DEFAULT 0");
        MigrateColumn(connection, "Projects", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        MigrateColumn(connection, "Goals", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        MigrateColumn(connection, "Flags", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        MigrateColumn(connection, "Columns", "DisplayName", "TEXT");
        MigrateColumn(connection, "People", "Email", "TEXT NULL");

        using (var backfillCmd = connection.CreateCommand())
        {
            backfillCmd.CommandText = "UPDATE Columns SET DisplayName = Name WHERE DisplayName IS NULL;";
            backfillCmd.ExecuteNonQuery();
        }

        // A recurring card sitting in Done (or already archived) must have already spawned its
        // next occurrence under the old always-spawn logic, even though NextOccurrenceSpawned - a
        // brand new column - backfilled to 0 for every pre-existing row. Without this, reactivating
        // one of those older completed recurring tasks and marking it Done again would still spawn
        // a duplicate, since nothing recorded that its first spawn already happened. Safe to run
        // every startup: once a row is flagged, this predicate no longer matches it.
        using (var recurringBackfillCmd = connection.CreateCommand())
        {
            recurringBackfillCmd.CommandText = """
                UPDATE Cards SET NextOccurrenceSpawned = 1
                WHERE IsRecurring = 1 AND NextOccurrenceSpawned = 0
                  AND (IsArchived = 1 OR ColumnId = (SELECT Id FROM Columns WHERE Name = 'Done' LIMIT 1));
                """;
            recurringBackfillCmd.ExecuteNonQuery();
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
}
