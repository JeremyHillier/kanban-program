using System.IO;
using KanbanApp.Services;

namespace KanbanApp.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    // A real task file: the copy is taken through SQLite's backup API, which needs one.
    private string TaskFile(string name)
    {
        var path = _temp.File(name);
        new DatabaseService(path).SetSetting("Marker", "task data");
        return path;
    }

    [Fact]
    public void Backup_CopiesTheTaskFileIntoABackupsFolderBesideIt()
    {
        var db = TaskFile("kanban.db");

        BackupService.CreateBackup(db, retentionCount: 20);

        var backup = Assert.Single(Directory.GetFiles(BackupService.GetBackupsDir(db)));
        Assert.StartsWith("kanban_", Path.GetFileName(backup));
        Assert.Equal("task data", new DatabaseService(backup).GetSetting("Marker"));   // a working copy, not just bytes
    }

    [Fact]
    public void Backup_KeepsOnlyTheNewestCopies()
    {
        var db = TaskFile("kanban.db");
        var backupsDir = Directory.CreateDirectory(BackupService.GetBackupsDir(db)).FullName;
        for (var day = 1; day <= 12; day++)
        {
            var old = Path.Combine(backupsDir, $"kanban_2026-01-{day:00}_120000.db");
            File.WriteAllText(old, $"day {day}");
            File.SetCreationTime(old, new DateTime(2026, 1, day));
        }
        var unrelated = Path.Combine(backupsDir, "something else.db");
        File.WriteAllText(unrelated, "not a backup of this file");

        BackupService.CreateBackup(db, retentionCount: 5);

        var kept = Directory.GetFiles(backupsDir, "kanban_*.db").Select(Path.GetFileName).ToList();
        Assert.Equal(5, kept.Count);
        Assert.Contains(kept, name => !name.StartsWith("kanban_2026-01-")); // the one just made
        Assert.DoesNotContain("kanban_2026-01-07_120000.db", kept);
        Assert.Contains("kanban_2026-01-12_120000.db", kept);
        Assert.True(File.Exists(unrelated), "files that aren't this task file's backups must be left alone");
    }

    [Fact]
    public void Backup_OfAMissingFile_DoesNothing()
    {
        var db = _temp.File("never-created.db");

        BackupService.CreateBackup(db, retentionCount: 20);

        Assert.False(Directory.Exists(BackupService.GetBackupsDir(db)));
    }
}

public sealed class AppConfigTests
{
    [Fact]
    public void SwitchTo_RemembersThePreviousFileAsMostRecent()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db" };

        config.SwitchTo(@"C:\files\b.db");
        config.SwitchTo(@"C:\files\c.db");

        Assert.Equal(@"C:\files\c.db", config.DbPath);
        Assert.Equal([@"C:\files\b.db", @"C:\files\a.db"], config.RecentDbPaths);
    }

    [Fact]
    public void SwitchTo_AFileAlreadyInTheList_MovesItOutOfTheListInsteadOfDuplicating()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db" };
        config.SwitchTo(@"C:\files\b.db");
        config.SwitchTo(@"C:\files\c.db");

        config.SwitchTo(@"c:\FILES\A.db");

        Assert.Equal([@"C:\files\c.db", @"C:\files\b.db"], config.RecentDbPaths);
    }

    [Fact]
    public void SwitchTo_TheCurrentFile_LeavesTheListUnchanged()
    {
        var config = new AppConfig { DbPath = @"C:\files\a.db" };

        config.SwitchTo(@"C:\files\a.db");

        Assert.Empty(config.RecentDbPaths);
    }

    [Fact]
    public void RecentList_IsCappedAtEight()
    {
        var config = new AppConfig { DbPath = @"C:\files\0.db" };
        for (var i = 1; i <= 12; i++) config.SwitchTo($@"C:\files\{i}.db");

        Assert.Equal(8, config.RecentDbPaths.Count);
        Assert.Equal(@"C:\files\11.db", config.RecentDbPaths[0]);
        Assert.Equal(@"C:\files\4.db", config.RecentDbPaths[^1]);
    }
}
