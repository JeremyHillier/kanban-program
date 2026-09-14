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
        foreach (var expected in new[] { "IsArchived", "Priority", "DueDate", "DueTime", "WhoId", "GoalId", "IsRecurring",
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
    public void BrandNewTaskFile_StartsWithTheDefaultBoard()
    {
        var db = new DatabaseService(_temp.File("new.db"));

        Assert.Equal(["To Do", "In Progress", "On Hold", "Waiting", "Done"], db.GetColumns().Select(c => c.Name));
        Assert.Equal("General", Assert.Single(db.GetProjects()).Name);
        Assert.Empty(db.GetCards());
    }
}
