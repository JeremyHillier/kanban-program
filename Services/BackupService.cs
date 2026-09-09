using System.IO;
using System.Linq;

namespace KanbanApp.Services;

// Writes a timestamped copy of the database on each app close (see MainWindow's Closing handler)
// and prunes anything beyond the configured retention count. Safe to call any time the app isn't
// mid-write: DatabaseService never holds a connection open across calls, so by the time the app is
// closing there's no in-progress transaction or journal file that could make a straight file copy
// inconsistent.
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
            File.Copy(dbPath, backupPath, overwrite: true);

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
