using System.IO;
using KanbanApp.Services;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Tests;

// The links between tables are declared and enforced, and a task file from before that was so is
// brought up to it once: backed up, tidied, rebuilt and checked - or, when that cannot be done,
// left exactly as it was.
public sealed class ForeignKeyTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    // Every link the app relies on: (table, column, parent, what happens when the parent goes).
    private static readonly (string Table, string Column, string Parent, string OnDelete)[] ExpectedLinks =
    [
        ("Cards", "ColumnId", "Columns", "RESTRICT"),
        ("Cards", "ProjectId", "Projects", "SET NULL"),
        ("Cards", "GoalId", "Goals", "SET NULL"),
        ("Cards", "WhoId", "People", "SET NULL"),
        ("SubTasks", "CardId", "Cards", "CASCADE"),
        ("CardFlags", "CardId", "Cards", "CASCADE"),
        ("CardFlags", "FlagId", "Flags", "CASCADE"),
        ("CardAttachments", "CardId", "Cards", "CASCADE"),
        ("CardPeople", "CardId", "Cards", "CASCADE"),
        ("CardPeople", "PersonId", "People", "CASCADE"),
    ];

    // The file as version 0.116 left it: every column, only the two links Cards was born with, and
    // a scattering of references that earlier versions could leave behind. Card ids run to 10 and
    // 10 was then deleted, so the next id must be 11 after the rebuild, not 10 again.
    private string CreateFileFromBeforeTheLinks(string name = "old.db")
    {
        var path = _temp.File(name);
        Run(path, """
            CREATE TABLE Columns (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1, DisplayName TEXT);
            CREATE TABLE Projects (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE Goals (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE Flags (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE People (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1, Email TEXT NULL);
            CREATE TABLE CardFlags (CardId INTEGER NOT NULL, FlagId INTEGER NOT NULL, PRIMARY KEY (CardId, FlagId));
            CREATE TABLE SubTasks (Id INTEGER PRIMARY KEY AUTOINCREMENT, CardId INTEGER NOT NULL, Title TEXT NOT NULL, IsDone INTEGER NOT NULL DEFAULT 0, SortOrder INTEGER NOT NULL);
            CREATE TABLE Cards (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, ColumnId INTEGER NOT NULL, Title TEXT NOT NULL, SortOrder INTEGER NOT NULL,
                ProjectId INTEGER NULL, IsArchived INTEGER NOT NULL DEFAULT 0, Priority TEXT NOT NULL DEFAULT 'Normal', DueDate TEXT NULL,
                Who TEXT NULL, LastUpdated TEXT NULL,
                FOREIGN KEY (ColumnId) REFERENCES Columns(Id) ON DELETE CASCADE,
                FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL);
            CREATE TABLE CardHistory (Id INTEGER PRIMARY KEY AUTOINCREMENT, CardId INTEGER NOT NULL, CardTitle TEXT NOT NULL, EventType TEXT NOT NULL, Details TEXT NOT NULL, Timestamp TEXT NOT NULL);
            CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
            CREATE TABLE CardAttachments (Id INTEGER PRIMARY KEY AUTOINCREMENT, CardId INTEGER NOT NULL, FilePath TEXT NOT NULL, DisplayName TEXT NOT NULL, AddedDate TEXT NOT NULL);
            CREATE TABLE CardPeople (CardId INTEGER NOT NULL, PersonId INTEGER NOT NULL, SortOrder INTEGER NOT NULL, PRIMARY KEY (CardId, PersonId));
            CREATE INDEX IX_CardPeople_Person ON CardPeople (PersonId);
            CREATE TABLE TaskTemplates (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Json TEXT NOT NULL);
            ALTER TABLE Cards ADD COLUMN IsRecurring INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Cards ADD COLUMN RecurrencePattern TEXT NULL;
            ALTER TABLE Cards ADD COLUMN GoalId INTEGER NULL;
            ALTER TABLE Cards ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Cards ADD COLUMN Notes TEXT NULL;
            ALTER TABLE Cards ADD COLUMN IsImported INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Cards ADD COLUMN WhoId INTEGER NULL;
            ALTER TABLE Cards ADD COLUMN ForceEditOnComplete INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Cards ADD COLUMN NextOccurrenceSpawned INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Cards ADD COLUMN WebsiteUrl TEXT NULL;
            ALTER TABLE Cards ADD COLUMN DueTime TEXT NULL;
            ALTER TABLE Cards ADD COLUMN StartDate TEXT NULL;
            ALTER TABLE Cards ADD COLUMN WaitingOn TEXT NULL;
            ALTER TABLE Cards ADD COLUMN RecurrencesLeft INTEGER NULL;
            ALTER TABLE Cards ADD COLUMN CompletedAt TEXT NULL;

            INSERT INTO Columns (Name, SortOrder, DisplayName) VALUES ('To Do', 0, 'To Do'), ('Doing', 1, 'Doing'), ('Done', 2, 'Done');
            INSERT INTO Projects (Name, SortOrder) VALUES ('Alpha', 0), ('Beta', 1);
            INSERT INTO Goals (Name, SortOrder) VALUES ('Grow', 0);
            INSERT INTO People (Name, SortOrder) VALUES ('Ann', 0), ('Bob', 1);
            INSERT INTO Flags (Name, SortOrder) VALUES ('Red', 0), ('Blue', 1);
            INSERT INTO Cards (Id, ColumnId, Title, SortOrder, ProjectId, GoalId, WhoId, Notes, DueDate) VALUES
                (1, 1, 'Sound', 0, 1, 1, 1, 'keep me', '2026-10-01'),
                (2, 2, 'Lost project', 0, 99, NULL, NULL, NULL, NULL),
                (3, 2, 'Lost goal', 1, 2, 77, NULL, NULL, NULL),
                (4, 3, 'Lost lead', 0, NULL, NULL, 55, NULL, NULL),
                (5, 44, 'Lost column', 0, NULL, NULL, NULL, NULL, NULL),
                (10, 1, 'Gone', 1, NULL, NULL, NULL, NULL, NULL);
            DELETE FROM Cards WHERE Id = 10;
            INSERT INTO SubTasks (CardId, Title, IsDone, SortOrder) VALUES (1, 'Step', 1, 0), (42, 'Orphan step', 0, 0);
            INSERT INTO CardFlags (CardId, FlagId) VALUES (1, 1), (1, 9), (42, 1);
            INSERT INTO CardAttachments (CardId, FilePath, DisplayName, AddedDate) VALUES (1, 'C:\a.txt', 'a.txt', '2026-09-01'), (42, 'C:\b.txt', 'b.txt', '2026-09-01');
            INSERT INTO CardPeople (CardId, PersonId, SortOrder) VALUES (1, 1, 0), (4, 55, 0), (4, 2, 1), (42, 1, 0);
            INSERT INTO CardHistory (CardId, CardTitle, EventType, Details, Timestamp) VALUES (42, 'Long gone', 'PermanentlyDeleted', 'kept as history', '2026-08-01 10:00:00');
            INSERT INTO Settings (Key, Value) VALUES ('Theme', 'Dark'), ('FileFormat', '3');
            """);
        return path;
    }

    // As an older copy of the app would write: with nothing enforced.
    private static void Run(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Foreign Keys=False");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static long Count(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return (long)cmd.ExecuteScalar()!;
    }

    private static List<(string Column, string Parent, string OnDelete)> LinksOf(string path, string table)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT \"from\", \"table\", on_delete FROM pragma_foreign_key_list('{table}');";
        using var reader = cmd.ExecuteReader();
        var links = new List<(string, string, string)>();
        while (reader.Read()) links.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return links;
    }

    private static void AssertEveryLinkDeclared(string path)
    {
        foreach (var (table, column, parent, onDelete) in ExpectedLinks)
        {
            Assert.Contains((column, parent, onDelete), LinksOf(path, table));
        }
    }

    private string[] BackupsBeforeUpdate(string path) =>
        Directory.Exists(BackupService.GetBackupsDir(path))
            ? Directory.GetFiles(BackupService.GetBackupsDir(path), $"*_{BackupService.BeforeUpdateTag}_*.db")
            : [];

    [Fact]
    public void ANewTaskFile_DeclaresEveryLink_AndEnforcesThem_WithNoBackupNeeded()
    {
        var path = _temp.File("new.db");

        var db = new DatabaseService(path);

        Assert.True(db.ForeignKeysEnforced);
        Assert.Null(db.ForeignKeyUpgradeProblem);
        AssertEveryLinkDeclared(path);
        Assert.Empty(BackupsBeforeUpdate(path));
        Assert.Null(db.GetSetting("ForeignKeysUpgrade"));

        // A sub-task can no longer be written for a task that does not exist.
        Assert.Throws<SqliteException>(() => db.SetCardSubTasks(9999, [("nowhere", false)]));
    }

    [Fact]
    public void AFileFromBeforeTheLinks_IsBackedUp_Tidied_AndRebuilt_WithEverythingElseKept()
    {
        var path = CreateFileFromBeforeTheLinks();

        var db = new DatabaseService(path);

        Assert.Null(db.ForeignKeyUpgradeProblem);
        Assert.True(db.ForeignKeysEnforced);
        AssertEveryLinkDeclared(path);

        // The backup is the file as it was, stray references and all.
        var backup = Assert.Single(BackupsBeforeUpdate(path));
        Assert.Equal(1, Count(backup, "SELECT COUNT(*) FROM SubTasks WHERE CardId = 42"));
        Assert.Equal(99, Count(backup, "SELECT ProjectId FROM Cards WHERE Id = 2"));

        // Every task is still there, with its id and its details.
        var cards = db.GetCards().OrderBy(c => c.Id).ToList();
        Assert.Equal([1, 2, 3, 4, 5], cards.Select(c => c.Id));
        Assert.Equal(["Sound", "Lost project", "Lost goal", "Lost lead", "Lost column"], cards.Select(c => c.Title));
        Assert.Equal("keep me", cards[0].Notes);
        Assert.Equal(new DateTime(2026, 10, 1), cards[0].DueDate);
        Assert.Equal(1, cards[0].ProjectId);
        Assert.Equal(1, cards[0].GoalId);
        Assert.Equal(1, cards[0].WhoId);

        // The stray references, each handled as the app would have when the thing was deleted.
        Assert.Null(cards[1].ProjectId);
        Assert.Null(cards[2].GoalId);
        Assert.Equal(2, cards[3].WhoId);                // the next person on its list steps up
        Assert.Equal(1, cards[4].ColumnId);             // moved to the first column
        Assert.Equal(1, Count(path, "SELECT COUNT(*) FROM SubTasks"));
        Assert.Equal(1, Count(path, "SELECT COUNT(*) FROM CardFlags"));
        Assert.Equal(1, Count(path, "SELECT COUNT(*) FROM CardAttachments"));
        Assert.Equal(2, Count(path, "SELECT COUNT(*) FROM CardPeople"));
        Assert.Equal(0, Count(path, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
        Assert.Equal(8, db.ForeignKeyRepairs.Count);

        // History is an audit trail and is left alone even for a task that is long gone.
        Assert.Equal(1, Count(path, "SELECT COUNT(*) FROM CardHistory WHERE CardId = 42"));
        Assert.Equal("Dark", db.GetSetting("Theme"));
        Assert.Equal(1, Count(path, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'IX_CardPeople_Person'"));

        // A deleted task's id is not handed out again.
        var added = db.AddCard(1, "After", null, "To Do", "Normal", null, null, false, null, null);
        Assert.Equal(11, added.Id);

        var note = db.GetSetting("ForeignKeysUpgrade");
        Assert.NotNull(note);
        Assert.Contains("rebuilt Cards, SubTasks, CardFlags, CardAttachments, CardPeople", note);
        Assert.Contains("tidied", note);
        Assert.Contains(Path.GetFileName(backup), note);
    }

    [Fact]
    public void OpeningAnUpgradedFileAgain_ChangesNothing_AndTakesNoSecondBackup()
    {
        var path = CreateFileFromBeforeTheLinks();
        _ = new DatabaseService(path);
        var note = new DatabaseService(path).GetSetting("ForeignKeysUpgrade");

        var again = new DatabaseService(path);

        Assert.True(again.ForeignKeysEnforced);
        Assert.Empty(again.ForeignKeyRepairs);
        Assert.Single(BackupsBeforeUpdate(path));
        Assert.Equal(note, again.GetSetting("ForeignKeysUpgrade"));
    }

    [Fact]
    public void AStrayReferenceWrittenLater_IsTidiedAfterABackup_NotLeftToBreakTheApp()
    {
        var path = CreateFileFromBeforeTheLinks();
        _ = new DatabaseService(path);
        SqliteConnection.ClearAllPools();
        // As an older copy of the app on another PC could: no enforcement on its connection.
        Run(path, "INSERT INTO SubTasks (CardId, Title, IsDone, SortOrder) VALUES (4242, 'late orphan', 0, 0);");

        var db = new DatabaseService(path);

        Assert.True(db.ForeignKeysEnforced);
        Assert.Equal(["1 sub-task(s) of a task that no longer exists"], db.ForeignKeyRepairs);
        Assert.Equal(2, BackupsBeforeUpdate(path).Length);
        Assert.Equal(0, Count(path, "SELECT COUNT(*) FROM SubTasks WHERE CardId = 4242"));
    }

    [Fact]
    public void WhenTheBackupCannotBeMade_TheFileIsLeftAlone_NothingIsEnforced_AndTheNextStartTriesAgain()
    {
        var path = CreateFileFromBeforeTheLinks();
        File.WriteAllText(BackupService.GetBackupsDir(path), "a file where the Backups folder should be");

        var db = new DatabaseService(path);

        Assert.False(db.ForeignKeysEnforced);
        Assert.NotNull(db.ForeignKeyUpgradeProblem);
        Assert.Contains("backup", db.ForeignKeyUpgradeProblem);
        Assert.Empty(LinksOf(path, "SubTasks"));
        Assert.Equal(1, Count(path, "SELECT COUNT(*) FROM SubTasks WHERE CardId = 42"));
        Assert.Equal(99, Count(path, "SELECT ProjectId FROM Cards WHERE Id = 2"));
        Assert.Null(db.GetSetting("ForeignKeysUpgrade"));
        db.SetCardSubTasks(4242, [("still allowed this session", false)]);   // no enforcement, as before

        SqliteConnection.ClearAllPools();
        File.Delete(BackupService.GetBackupsDir(path));
        var next = new DatabaseService(path);

        Assert.True(next.ForeignKeysEnforced);
        Assert.Null(next.ForeignKeyUpgradeProblem);
        AssertEveryLinkDeclared(path);
        Assert.Single(BackupsBeforeUpdate(path));
    }

    [Fact]
    public void AFileWithColumnsThisVersionDoesNotKnow_IsNotRebuilt()
    {
        var path = CreateFileFromBeforeTheLinks();
        Run(path, "ALTER TABLE Cards ADD COLUMN FromTheFuture TEXT NULL; UPDATE Cards SET FromTheFuture = 'precious' WHERE Id = 1;");

        var db = new DatabaseService(path);

        Assert.False(db.ForeignKeysEnforced);
        Assert.Contains("newer version", db.ForeignKeyUpgradeProblem);
        Assert.Contains("Cards.FromTheFuture", db.ForeignKeyUpgradeProblem);
        Assert.Empty(BackupsBeforeUpdate(path));
        Assert.Equal(1, Count(path, "SELECT COUNT(*) FROM Cards WHERE FromTheFuture = 'precious'"));
        Assert.Equal("Sound", Assert.Single(db.GetCards(), c => c.Id == 1).Title);
    }

    [Fact]
    public void TheLinksHoldOnEveryConnectionTheAppOpens()
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var card = db.AddCard(db.GetColumns()[0].Id, "Linked", null, "To Do", "Normal", null, null, false, null, null);

        Assert.Throws<SqliteException>(() => db.SetCardFlags(card.Id, [12345]));
        Assert.Throws<SqliteException>(() => db.SetCardPeople(card.Id, [12345]));
        Assert.Throws<SqliteException>(() => db.SetCardAttachments(12345, [("C:\\x.txt", "x.txt", DateTime.Today)]));

        // The app's own deletes still work: the project goes and the task simply loses the link.
        var project = db.AddProject("Short-lived");
        db.UpdateCard(card.Id, "Linked", project.Id, "Normal", null, null, false, null, null);
        db.DeleteProject(project.Id);
        Assert.Null(Assert.Single(db.GetCards(), c => c.Id == card.Id).ProjectId);
    }
}
