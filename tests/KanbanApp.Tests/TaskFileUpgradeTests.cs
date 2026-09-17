using KanbanApp.Services;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Tests;

// A customer's task file can be many versions old when they install an update. Opening it must add
// whatever the newer version needs without losing or mangling anything already in it.
public sealed class TaskFileUpgradeTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    // Roughly the first shipped format: no People, Goals, flags, sub-tasks, attachments, times, etc.
    private string CreateOldFormatTaskFile()
    {
        var path = _temp.File("old.db");
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Columns (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL);
            CREATE TABLE Projects (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL);
            CREATE TABLE Cards (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, ColumnId INTEGER NOT NULL, Title TEXT NOT NULL,
                SortOrder INTEGER NOT NULL, ProjectId INTEGER NULL);
            CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);

            INSERT INTO Columns (Name, SortOrder) VALUES ('To Do', 0), ('In Progress', 1), ('Done', 2);
            INSERT INTO Projects (Name, SortOrder) VALUES ('Legacy Project', 0);
            INSERT INTO Cards (ColumnId, Title, SortOrder, ProjectId) VALUES (2, 'Old task', 0, 1);
            INSERT INTO Settings (Key, Value) VALUES ('Theme', 'Dark');
            """;
        cmd.ExecuteNonQuery();
        return path;
    }

    private static List<string> ColumnNames(string dbPath, string table)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(1));
        return names;
    }

    [Fact]
    public void OldTaskFile_OpensWithItsDataIntact()
    {
        var path = CreateOldFormatTaskFile();

        var db = new DatabaseService(path);

        var card = Assert.Single(db.GetCards());
        Assert.Equal("Old task", card.Title);
        Assert.Equal(1, card.ProjectId);
        Assert.Equal("Normal", card.Priority);
        Assert.Null(card.DueDate);
        Assert.Null(card.DueTime);
        Assert.False(card.IsRecurring);
        Assert.Null(card.Notes);
        Assert.Empty(card.SubTasks);

        Assert.Equal(["To Do", "In Progress", "Done"], db.GetColumns().Select(c => c.Name));
        Assert.Equal(["To Do", "In Progress", "Done"], db.GetColumns().Select(c => c.DisplayName));
        Assert.Equal("Legacy Project", Assert.Single(db.GetProjects()).Name);
        Assert.Equal("Dark", db.GetSetting("Theme"));
    }

    [Fact]
    public void OldTaskFile_GainsEveryColumnTheCurrentVersionUses()
    {
        var path = CreateOldFormatTaskFile();

        _ = new DatabaseService(path);

        var cardColumns = ColumnNames(path, "Cards");
        foreach (var expected in new[] { "IsArchived", "Priority", "DueDate", "DueTime", "CompletedAt", "WhoId", "GoalId", "IsRecurring",
                     "RecurrencePattern", "NextOccurrenceSpawned", "IsDeleted", "Notes", "IsImported", "ForceEditOnComplete", "WebsiteUrl" })
        {
            Assert.Contains(expected, cardColumns);
        }
        Assert.Contains("DisplayName", ColumnNames(path, "Columns"));
        Assert.Contains("IsActive", ColumnNames(path, "Projects"));
        Assert.Contains("Email", ColumnNames(path, "People"));
    }

    [Fact]
    public void OpeningTheSameFileRepeatedly_ChangesNothingFurther()
    {
        var path = CreateOldFormatTaskFile();

        _ = new DatabaseService(path);
        SqliteConnection.ClearAllPools();
        var db = new DatabaseService(path);

        Assert.Single(db.GetCards());
        Assert.Equal(3, db.GetColumns().Count);
        Assert.Single(db.GetProjects());
    }

    [Fact]
    public void LegacyFreeTextWho_BecomesAPersonTheCardPointsTo()
    {
        var path = _temp.File("who.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE Columns (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL);
                CREATE TABLE Cards (Id INTEGER PRIMARY KEY AUTOINCREMENT, ColumnId INTEGER NOT NULL, Title TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL, Who TEXT NULL);
                INSERT INTO Columns (Name, SortOrder) VALUES ('To Do', 0);
                INSERT INTO Cards (ColumnId, Title, SortOrder, Who) VALUES (1, 'A', 0, 'Pat'), (1, 'B', 1, 'pat '), (1, 'C', 2, NULL);
                """;
            cmd.ExecuteNonQuery();
        }

        var db = new DatabaseService(path);

        var person = Assert.Single(db.GetPeople());
        Assert.Equal("Pat", person.Name);
        var cards = db.GetCards().ToDictionary(c => c.Title);
        Assert.Equal(person.Id, cards["A"].WhoId);
        Assert.Null(cards["C"].WhoId);
    }

    [Fact]
    public void TasksAlreadyDone_GetTheirCompletionTimeFromHistory()
    {
        var path = _temp.File("history.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE Columns (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL);
                CREATE TABLE Cards (Id INTEGER PRIMARY KEY AUTOINCREMENT, ColumnId INTEGER NOT NULL, Title TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL, IsArchived INTEGER NOT NULL DEFAULT 0, LastUpdated TEXT NULL);
                CREATE TABLE CardHistory (Id INTEGER PRIMARY KEY AUTOINCREMENT, CardId INTEGER NOT NULL, CardTitle TEXT NOT NULL,
                    EventType TEXT NOT NULL, Details TEXT NOT NULL, Timestamp TEXT NOT NULL);
                INSERT INTO Columns (Name, SortOrder) VALUES ('To Do', 0), ('Done', 1);

                -- 1: finished twice (reopened in between) and then edited: the latest arrival in Done counts, not the edit.
                INSERT INTO Cards (ColumnId, Title, SortOrder, LastUpdated) VALUES (2, 'Done twice', 0, '2026-03-09 17:00:00');
                INSERT INTO CardHistory (CardId, CardTitle, EventType, Details, Timestamp) VALUES
                    (1, 'Done twice', 'Moved', 'Moved from To Do to Done', '2026-03-01 09:00:00'),
                    (1, 'Done twice', 'Moved', 'Moved from Done to To Do', '2026-03-02 09:00:00'),
                    (1, 'Done twice', 'Moved', 'Moved from To Do to Done', '2026-03-05 14:30:00'),
                    (1, 'Done twice', 'Edited', 'Task details updated', '2026-03-09 17:00:00');
                -- 2: created straight into Done, then archived.
                INSERT INTO Cards (ColumnId, Title, SortOrder, IsArchived, LastUpdated) VALUES (2, 'Created done', 1, 1, '2026-04-02 08:00:00');
                INSERT INTO CardHistory (CardId, CardTitle, EventType, Details, Timestamp) VALUES
                    (2, 'Created done', 'Created', 'Added to Done', '2026-04-01 10:15:00'),
                    (2, 'Created done', 'Archived', 'Archived from Done', '2026-04-02 08:00:00');
                -- 3: archived, with no record of arriving in Done: falls back to the archive time.
                INSERT INTO Cards (ColumnId, Title, SortOrder, IsArchived, LastUpdated) VALUES (2, 'No arrival record', 2, 1, '2026-05-10 12:00:00');
                INSERT INTO CardHistory (CardId, CardTitle, EventType, Details, Timestamp) VALUES
                    (3, 'No arrival record', 'Archived', 'Archived from Done', '2026-05-03 11:00:00');
                -- 4: in Done with no history at all: last updated is the best available.
                INSERT INTO Cards (ColumnId, Title, SortOrder, LastUpdated) VALUES (2, 'No history', 3, '2026-06-06 06:06:00');
                -- 5: not finished: stays empty even though it once passed through Done.
                INSERT INTO Cards (ColumnId, Title, SortOrder, LastUpdated) VALUES (1, 'Reopened', 0, '2026-07-01 00:00:00');
                INSERT INTO CardHistory (CardId, CardTitle, EventType, Details, Timestamp) VALUES
                    (5, 'Reopened', 'Moved', 'Moved from To Do to Done', '2026-06-20 10:00:00'),
                    (5, 'Reopened', 'Moved', 'Moved from Done to To Do', '2026-06-21 10:00:00');
                """;
            cmd.ExecuteNonQuery();
        }

        var db = new DatabaseService(path);

        var board = db.GetCards().Concat(db.GetCards(archivedOnly: true)).ToDictionary(c => c.Title, c => c.CompletedAt);
        Assert.Equal(new DateTime(2026, 3, 5, 14, 30, 0), board["Done twice"]);
        Assert.Equal(new DateTime(2026, 4, 1, 10, 15, 0), board["Created done"]);
        Assert.Equal(new DateTime(2026, 5, 3, 11, 0, 0), board["No arrival record"]);
        Assert.Equal(new DateTime(2026, 6, 6, 6, 6, 0), board["No history"]);
        Assert.Null(board["Reopened"]);
    }

    [Fact]
    public void BrandNewTaskFile_StartsWithTheDefaultBoard()
    {
        var db = new DatabaseService(_temp.File("new.db"));

        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "Done"], db.GetColumns().Select(c => c.Name));
        Assert.Equal("General", Assert.Single(db.GetProjects()).Name);
        Assert.Empty(db.GetCards());
    }

    private static string QueryPlan(string dbPath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
        using var reader = cmd.ExecuteReader();
        var steps = new List<string>();
        while (reader.Read()) steps.Add(reader.GetString(3));
        return string.Join(" | ", steps);
    }

    [Fact]
    public void OldTaskFile_GetsTheHistoryIndex_AndTheArchivedLookupUsesIt()
    {
        var path = CreateOldFormatTaskFile();

        _ = new DatabaseService(path);

        // The per-card "latest archive time" lookup used by the Archived list and reports must be
        // answered from the index, not by scanning all of CardHistory once per card.
        var plan = QueryPlan(path,
            "SELECT (SELECT MAX(h.Timestamp) FROM CardHistory h WHERE h.CardId = c.Id AND h.EventType = 'Archived') FROM Cards c;");
        Assert.Contains("IX_CardHistory_Card_Event_Time", plan);
        Assert.DoesNotContain("SCAN h", plan);
    }

    [Fact]
    public void ArchivedTasks_StillReportTheirLatestArchiveTime()
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var done = db.GetColumns().Single(c => c.Name == "Done");
        var card = db.AddCard(done.Id, "Finished", null, "Done", "Normal", null, null, false, null, null);

        db.ArchiveCard(card.Id, card.Title, "Done");

        var archived = Assert.Single(db.GetCards(archivedOnly: true));
        Assert.NotNull(archived.ArchivedAt);
        Assert.Equal(card.Id, Assert.Single(db.GetArchivedCards()).Id);
    }
}
