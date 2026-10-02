using System.IO;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// The links between tables - a task's column, project, goal and lead person; a sub-task's,
// attachment's, flag's or person's task - are declared as foreign keys and enforced on every
// connection the app opens, so no code path can leave a row pointing at something that is not there.
//
// SQLite cannot add a constraint to an existing table, so a task file from before this was so is
// upgraded once: a backup is taken first, references left dangling by earlier versions are tidied,
// each table concerned is rebuilt with its constraints (same columns, same ids, same next id), and
// the whole file is checked before the change is kept. If any step cannot be done the file is left
// exactly as it was, nothing is enforced this session, and ForeignKeyUpgradeProblem says why.
public partial class DatabaseService
{
    private sealed record ForeignKey(string Column, string Parent, string OnDelete)
    {
        public string Sql => $"FOREIGN KEY ({Column}) REFERENCES {Parent}(Id) ON DELETE {OnDelete}";
    }

    // A table's full definition. Cards carries every column ever added to it, so a new file gets
    // them all at once (the MigrateColumn calls then find nothing to do) and an upgraded file is
    // rebuilt to the same shape.
    private sealed record TableShape(string Name, string[] Columns, ForeignKey[] ForeignKeys, string[] Indexes, bool HasOwnIdSequence)
    {
        public IEnumerable<string> ColumnNames => Columns.Where(c => !c.StartsWith("PRIMARY KEY (", StringComparison.Ordinal)).Select(c => c[..c.IndexOf(' ')]);

        public string CreateSql(string asName, bool ifNotExists) =>
            $"CREATE TABLE {(ifNotExists ? "IF NOT EXISTS " : "")}{asName} (\n    " +
            string.Join(",\n    ", Columns.Concat(ForeignKeys.Select(f => f.Sql))) + "\n);";
    }

    private static readonly TableShape[] LinkedTables =
    [
        new("Cards",
            [
                "Id INTEGER PRIMARY KEY AUTOINCREMENT",
                "ColumnId INTEGER NOT NULL",
                "Title TEXT NOT NULL",
                "SortOrder INTEGER NOT NULL",
                "ProjectId INTEGER NULL",
                "IsArchived INTEGER NOT NULL DEFAULT 0",
                "Priority TEXT NOT NULL DEFAULT 'Normal'",
                "DueDate TEXT NULL",
                "Who TEXT NULL",
                "LastUpdated TEXT NULL",
                "IsRecurring INTEGER NOT NULL DEFAULT 0",
                "RecurrencePattern TEXT NULL",
                "GoalId INTEGER NULL",
                "IsDeleted INTEGER NOT NULL DEFAULT 0",
                "Notes TEXT NULL",
                "IsImported INTEGER NOT NULL DEFAULT 0",
                "WhoId INTEGER NULL",
                "ForceEditOnComplete INTEGER NOT NULL DEFAULT 0",
                "NextOccurrenceSpawned INTEGER NOT NULL DEFAULT 0",
                "WebsiteUrl TEXT NULL",
                "DueTime TEXT NULL",
                "StartDate TEXT NULL",
                "WaitingOn TEXT NULL",
                "RecurrencesLeft INTEGER NULL",
                "CompletedAt TEXT NULL",
                "ShareId TEXT NULL",
            ],
            [
                // A column with tasks in it cannot be deleted; a project, goal or person can, and
                // the tasks that pointed at it simply lose the link (the app clears it first anyway).
                new("ColumnId", "Columns", "RESTRICT"),
                new("ProjectId", "Projects", "SET NULL"),
                new("GoalId", "Goals", "SET NULL"),
                new("WhoId", "People", "SET NULL"),
            ],
            [], HasOwnIdSequence: true),
        new("SubTasks",
            [
                "Id INTEGER PRIMARY KEY AUTOINCREMENT",
                "CardId INTEGER NOT NULL",
                "Title TEXT NOT NULL",
                "IsDone INTEGER NOT NULL DEFAULT 0",
                "SortOrder INTEGER NOT NULL",
            ],
            [new("CardId", "Cards", "CASCADE")], [], HasOwnIdSequence: true),
        new("CardFlags",
            [
                "CardId INTEGER NOT NULL",
                "FlagId INTEGER NOT NULL",
                "PRIMARY KEY (CardId, FlagId)",
            ],
            [new("CardId", "Cards", "CASCADE"), new("FlagId", "Flags", "CASCADE")], [], HasOwnIdSequence: false),
        new("CardAttachments",
            [
                "Id INTEGER PRIMARY KEY AUTOINCREMENT",
                "CardId INTEGER NOT NULL",
                "FilePath TEXT NOT NULL",
                "DisplayName TEXT NOT NULL",
                "AddedDate TEXT NOT NULL",
            ],
            [new("CardId", "Cards", "CASCADE")], [], HasOwnIdSequence: true),
        new("CardPeople",
            [
                "CardId INTEGER NOT NULL",
                "PersonId INTEGER NOT NULL",
                "SortOrder INTEGER NOT NULL",
                "PRIMARY KEY (CardId, PersonId)",
            ],
            [new("CardId", "Cards", "CASCADE"), new("PersonId", "People", "CASCADE")],
            ["CREATE INDEX IF NOT EXISTS IX_CardPeople_Person ON CardPeople (PersonId);"], HasOwnIdSequence: false),
    ];

    private static TableShape Shape(string table) => LinkedTables.Single(t => t.Name == table);

    // References an earlier version could leave behind, and what is done about each. Rows that hang
    // off a task that no longer exists are unreachable from the app and go; a task pointing at a
    // project, goal or person that no longer exists loses the link, as it would have when that
    // thing was deleted; a task whose column is gone would never show, so it moves to the first
    // column. In this order: a task's lead is taken from its list of people, so that list is
    // tidied first.
    private static readonly (string What, string Sql)[] ReferenceRepairs =
    [
        ("flag(s) on a task, or of a flag, that no longer exists",
            "DELETE FROM CardFlags WHERE CardId NOT IN (SELECT Id FROM Cards) OR FlagId NOT IN (SELECT Id FROM Flags);"),
        ("person entry(ies) on a task, or of a person, that no longer exists",
            "DELETE FROM CardPeople WHERE CardId NOT IN (SELECT Id FROM Cards) OR PersonId NOT IN (SELECT Id FROM People);"),
        ("sub-task(s) of a task that no longer exists",
            "DELETE FROM SubTasks WHERE CardId NOT IN (SELECT Id FROM Cards);"),
        ("attachment(s) of a task that no longer exists",
            "DELETE FROM CardAttachments WHERE CardId NOT IN (SELECT Id FROM Cards);"),
        ("task(s) pointing at a project that no longer exists",
            "UPDATE Cards SET ProjectId = NULL WHERE ProjectId IS NOT NULL AND ProjectId NOT IN (SELECT Id FROM Projects);"),
        ("task(s) pointing at a goal that no longer exists",
            "UPDATE Cards SET GoalId = NULL WHERE GoalId IS NOT NULL AND GoalId NOT IN (SELECT Id FROM Goals);"),
        ("task(s) whose lead person no longer exists",
            "UPDATE Cards SET WhoId = (SELECT PersonId FROM CardPeople WHERE CardId = Cards.Id ORDER BY SortOrder LIMIT 1) " +
            "WHERE WhoId IS NOT NULL AND WhoId NOT IN (SELECT Id FROM People);"),
        ("task(s) in a column that no longer exists, moved to the first column",
            "UPDATE Cards SET ColumnId = (SELECT Id FROM Columns ORDER BY SortOrder LIMIT 1) " +
            "WHERE ColumnId NOT IN (SELECT Id FROM Columns) AND EXISTS (SELECT 1 FROM Columns);"),
    ];

    private const string ForeignKeysUpgradeKey = "ForeignKeysUpgrade";

    // True once every connection the app opens enforces the links. False only while a task file
    // could not be upgraded (see ForeignKeyUpgradeProblem).
    public bool ForeignKeysEnforced { get; private set; }

    // Why the task file was not upgraded and the links are not being enforced this session, in the
    // user's terms; null when all is well. The app shows it once at startup.
    public string? ForeignKeyUpgradeProblem { get; private set; }

    // What the upgrade tidied, one line per kind of stray reference found; empty when there were none.
    internal IReadOnlyList<string> ForeignKeyRepairs { get; private set; } = [];

    // Called first thing in Initialize, before anything else touches the file: decides whether the
    // tables need rebuilding and, if so, takes the backup while the file is still as it was.
    private ForeignKeyUpgrade BeginForeignKeyUpgrade(SqliteConnection connection)
    {
        if (!TableExists(connection, "Cards")) return ForeignKeyUpgrade.NothingYet;   // a new file: created with its constraints below

        var unknown = LinkedTables.Where(t => TableExists(connection, t.Name))
            .SelectMany(t => ColumnsOf(connection, t.Name).Except(t.ColumnNames).Select(c => $"{t.Name}.{c}"))
            .ToList();
        if (unknown.Count > 0)
        {
            return ForeignKeyUpgrade.Problem(
                "This task file has been used by a newer version of the app, which stores details this version does not know about " +
                $"({string.Join(", ", unknown)}). The file has been left as it is. Update this PC from the download page.");
        }

        if (LinkedTables.All(t => !TableExists(connection, t.Name) || HasItsForeignKeys(connection, t))) return ForeignKeyUpgrade.NothingYet;

        return TakeBackup(ForeignKeyUpgrade.NothingYet);
    }

    private ForeignKeyUpgrade TakeBackup(ForeignKeyUpgrade upgrade)
    {
        try
        {
            return upgrade with { BackupPath = BackupService.CreateBackupBeforeUpdate(DbPath) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            return ForeignKeyUpgrade.Problem(
                "The task file needs a one-time update to its structure, and the app takes a backup of it first. That backup could not be made, " +
                "so the update has not been done and the file is as it was. Check the Backups folder beside the task file, then start the app again.\n\n" + ex.Message);
        }
    }

    // Called once every table exists and has every column: does the rebuild the backup was taken
    // for, or, on a file already upgraded, checks that nothing written since (by an older copy of the
    // app, say) has left a stray reference - and tidies it, after a backup, if so.
    private void FinishForeignKeyUpgrade(SqliteConnection connection, ForeignKeyUpgrade upgrade)
    {
        if (upgrade.ProblemText is not null)
        {
            ForeignKeyUpgradeProblem = upgrade.ProblemText;
            return;
        }

        var toRebuild = LinkedTables.Where(t => !HasItsForeignKeys(connection, t)).ToList();
        if (toRebuild.Count == 0 && CountViolations(connection) == 0)
        {
            ForeignKeysEnforced = true;
            return;
        }

        if (upgrade.BackupPath is null)
        {
            // A file that already had its constraints, but with a stray reference in it - so no backup was taken up front.
            upgrade = TakeBackup(upgrade);
            if (upgrade.ProblemText is not null)
            {
                ForeignKeyUpgradeProblem = upgrade.ProblemText;
                return;
            }
        }

        using var transaction = connection.BeginTransaction();
        var repairs = new List<string>();
        foreach (var (what, sql) in ReferenceRepairs)
        {
            var count = Execute(connection, sql);
            if (count > 0) repairs.Add($"{count} {what}");
        }
        foreach (var table in toRebuild) Rebuild(connection, table);

        var violations = CountViolations(connection);
        if (violations > 0)
        {
            transaction.Rollback();
            ForeignKeyUpgradeProblem =
                $"The task file could not be updated: {violations} reference(s) in it still point at something that is not there, and the app would rather leave " +
                $"the file as it is than guess. It is safe to keep using. Please email a copy of the task file to {AppInfo.SupportEmail}.";
            return;
        }

        var note = $"{NowStamp()} by {RunningAppVersion}: " +
                   (toRebuild.Count > 0 ? $"rebuilt {string.Join(", ", toRebuild.Select(t => t.Name))}; " : "") +
                   (repairs.Count > 0 ? $"tidied {string.Join("; ", repairs)}; " : "nothing to tidy; ") +
                   $"backup {Path.GetFileName(upgrade.BackupPath)}";
        Execute(connection, "INSERT INTO Settings (Key, Value) VALUES ($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = $value;",
            ("$key", ForeignKeysUpgradeKey), ("$value", note));
        transaction.Commit();

        ForeignKeyRepairs = repairs;
        ForeignKeysEnforced = true;
    }

    // The documented way to change a table's constraints: build the new table, copy every row
    // across by name (ids included), drop the old one, take its name and its indexes, and carry its
    // next-id counter over so a deleted task's id is never reused for a new one (the history log
    // still refers to it).
    private static void Rebuild(SqliteConnection connection, TableShape shape)
    {
        var columns = string.Join(", ", shape.ColumnNames);
        var nextId = shape.HasOwnIdSequence
            ? Scalar(connection, "SELECT seq FROM sqlite_sequence WHERE name = $name;", ("$name", shape.Name)) as long?
            : null;

        Execute(connection, shape.CreateSql(shape.Name + "_new", ifNotExists: false));
        Execute(connection, $"INSERT INTO {shape.Name}_new ({columns}) SELECT {columns} FROM {shape.Name};");
        Execute(connection, $"DROP TABLE {shape.Name};");
        Execute(connection, $"ALTER TABLE {shape.Name}_new RENAME TO {shape.Name};");
        foreach (var index in shape.Indexes) Execute(connection, index);

        if (nextId is { } seq)
        {
            Execute(connection,
                "INSERT INTO sqlite_sequence (name, seq) SELECT $name, $seq WHERE NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = $name); " +
                "UPDATE sqlite_sequence SET seq = $seq WHERE name = $name AND seq < $seq;",
                ("$name", shape.Name), ("$seq", seq));
        }
    }

    private static bool HasItsForeignKeys(SqliteConnection connection, TableShape shape)
    {
        var declared = new List<(string Column, string Parent, string OnDelete)>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT \"from\", \"table\", on_delete FROM pragma_foreign_key_list($table);";
        cmd.Parameters.AddWithValue("$table", shape.Name);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) declared.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));

        return shape.ForeignKeys.All(f => declared.Contains((f.Column, f.Parent, f.OnDelete)));
    }

    private static long CountViolations(SqliteConnection connection) =>
        (long)Scalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;")!;

    private static bool TableExists(SqliteConnection connection, string table) =>
        (long)Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;", ("$name", table))! > 0;

    private static List<string> ColumnsOf(SqliteConnection connection, string table)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info($table);";
        cmd.Parameters.AddWithValue("$table", table);
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }

    private static object? Scalar(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteScalar();
    }

    // Where the upgrade has got to: a backup taken (and so a rebuild owed), or a reason it cannot
    // go ahead, or neither yet.
    private sealed record ForeignKeyUpgrade(string? BackupPath, string? ProblemText)
    {
        public static readonly ForeignKeyUpgrade NothingYet = new(null, null);
        public static ForeignKeyUpgrade Problem(string text) => new(null, text);
    }
}
