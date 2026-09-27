using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Writes a timestamped copy of the database on each app close (see MainWindow's Closing handler)
// and prunes anything beyond the configured retention count. The copy is taken through SQLite's own
// backup API, so it is a consistent database whatever else has the file open at the time (a sync
// client, or Back Up Now while a change is being written); a plain file copy could catch a
// half-written page.
public static class BackupService
{
    // In the name of the copy taken just before the app changes the task file's structure. Such a
    // copy is never pruned: it is the one to go back to if the change turns out wrong, however many
    // ordinary backups come after it.
    public const string BeforeUpdateTag = "before-update";

    public static string GetBackupsDir(string dbPath) => Path.Combine(Path.GetDirectoryName(dbPath)!, "Backups");

    public static void CreateBackup(string dbPath, int retentionCount)
    {
        try
        {
            if (!File.Exists(dbPath)) return;

            var backupsDir = GetBackupsDir(dbPath);
            Directory.CreateDirectory(backupsDir);

            var baseName = Path.GetFileNameWithoutExtension(dbPath);
            Copy(dbPath, Path.Combine(backupsDir, $"{baseName}_{DateTime.Now:yyyy-MM-dd_HHmmss}.db"));

            PruneOldBackups(backupsDir, baseName, retentionCount);
        }
        catch
        {
            // Best-effort - a backup problem should never stop the app from closing.
        }
    }

    // The copy before a structure change. Unlike the close-time backup this one throws when it
    // cannot be made: the caller must not go on without it.
    public static string CreateBackupBeforeUpdate(string dbPath)
    {
        var backupsDir = GetBackupsDir(dbPath);
        Directory.CreateDirectory(backupsDir);

        // Never over an earlier one, even in the same second: each is a state to go back to.
        var stem = Path.Combine(backupsDir, $"{Path.GetFileNameWithoutExtension(dbPath)}_{BeforeUpdateTag}_{DateTime.Now:yyyy-MM-dd_HHmmss}");
        var backupPath = stem + ".db";
        for (var n = 2; File.Exists(backupPath); n++) backupPath = $"{stem}-{n}.db";
        Copy(dbPath, backupPath);
        return backupPath;
    }

    private static void Copy(string dbPath, string backupPath)
    {
        if (File.Exists(backupPath)) File.Delete(backupPath);
        using var source = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        using var target = new SqliteConnection($"Data Source={backupPath};Pooling=False");
        source.Open();
        target.Open();
        source.BackupDatabase(target);
    }

    private static void PruneOldBackups(string backupsDir, string baseName, int retentionCount)
    {
        if (retentionCount <= 0) return;

        var backups = Directory.GetFiles(backupsDir, $"{baseName}_*.db")
            .Select(p => new FileInfo(p))
            .Where(f => !f.Name.Contains(BeforeUpdateTag, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        foreach (var stale in backups.Skip(retentionCount))
        {
            stale.Delete();
        }
    }
}
