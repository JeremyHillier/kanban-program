using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Writes a timestamped copy of the database on each app close (see MainWindow's Closing handler)
// and prunes anything beyond the configured retention count. The copy is taken through SQLite's own
// backup API, as the Personal Finance and Accounting programs do, so it is a consistent database
// whatever else has the file open at the time (a sync client, or Back Up Now while a change is
// being written); a plain file copy could catch a half-written page.
public static class BackupService
{
    public static string GetBackupsDir(string dbPath) => Path.Combine(Path.GetDirectoryName(dbPath)!, "Backups");

    public static void CreateBackup(string dbPath, int retentionCount)
    {
        try
        {
            if (!File.Exists(dbPath)) return;

            var backupsDir = GetBackupsDir(dbPath);
            Directory.CreateDirectory(backupsDir);

            var baseName = Path.GetFileNameWithoutExtension(dbPath);
            var backupPath = Path.Combine(backupsDir, $"{baseName}_{DateTime.Now:yyyy-MM-dd_HHmmss}.db");
            if (File.Exists(backupPath)) File.Delete(backupPath);
            using (var source = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            using (var target = new SqliteConnection($"Data Source={backupPath};Pooling=False"))
            {
                source.Open();
                target.Open();
                source.BackupDatabase(target);
            }

            PruneOldBackups(backupsDir, baseName, retentionCount);
        }
        catch
        {
            // Best-effort - a backup problem should never stop the app from closing.
        }
    }

    private static void PruneOldBackups(string backupsDir, string baseName, int retentionCount)
    {
        if (retentionCount <= 0) return;

        var backups = Directory.GetFiles(backupsDir, $"{baseName}_*.db")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        foreach (var stale in backups.Skip(retentionCount))
        {
            stale.Delete();
        }
    }
}
