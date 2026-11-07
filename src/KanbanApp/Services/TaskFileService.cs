using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace KanbanApp.Services;

// Whole task files: copying one safely while it may be open, duplicating one as a separate board,
// moving one with its attachments, and sending one to the Recycle Bin. The catalogue of files is
// AppConfig; what is inside a file is DatabaseService.
public static class TaskFileService
{
    public const string FileFilter = "Task File (*.db)|*.db|All Files (*.*)|*.*";

    // A SQLite-safe copy (the online backup API, as BackupService uses), so a file that is open in
    // the app copies whole. The Attachments folder beside it is copied too.
    public static void CopyTaskFile(string sourcePath, string destinationPath)
    {
        if (AppConfig.ArePathsEqual(sourcePath, destinationPath)) throw new IOException("The copy would be the file itself.");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using (var source = new SqliteConnection($"Data Source={sourcePath};Pooling=False"))
        using (var target = new SqliteConnection($"Data Source={destinationPath};Pooling=False"))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        var attachments = DatabaseService.GetAttachmentsDir(sourcePath);
        if (Directory.Exists(attachments)) CopyDirectory(attachments, DatabaseService.GetAttachmentsDir(destinationPath));
    }

    // A copy that is its own board from now on: same tasks, new identity, so the app can tell the two
    // apart (registering a moved file is a transfer; this is a duplicate).
    public static void DuplicateAsNewFile(string sourcePath, string destinationPath)
    {
        CopyTaskFile(sourcePath, destinationPath);
        new DatabaseService(destinationPath).AssignNewFileId();
        SqliteConnection.ClearAllPools();
    }

    // Moves a file that is not open, with its Attachments and Backups folders.
    public static void MoveTaskFile(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        SqliteConnection.ClearAllPools();
        File.Move(sourcePath, destinationPath);
        MoveFolder(DatabaseService.GetAttachmentsDir(sourcePath), DatabaseService.GetAttachmentsDir(destinationPath));
        MoveFolder(BackupService.GetBackupsDir(sourcePath), BackupService.GetBackupsDir(destinationPath));
    }

    // The file and the folders that belong to it (Attachments, Backups) go to the Recycle Bin, where
    // they can be put back; nothing is destroyed outright.
    public static void SendToRecycleBin(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var item in new[] { dbPath, DatabaseService.GetAttachmentsDir(dbPath), BackupService.GetBackupsDir(dbPath) })
        {
            if (File.Exists(item) || Directory.Exists(item)) Recycle(item);
        }
    }

    // Tells the user when a file lives in a synced folder, where two PCs editing their own copies
    // of one SQLite file would silently overwrite each other's work.
    public static bool LooksSynced(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)
            || full.Contains("Dropbox", StringComparison.OrdinalIgnoreCase)
            || full.Contains("Google Drive", StringComparison.OrdinalIgnoreCase)
            || full.Contains("iCloudDrive", StringComparison.OrdinalIgnoreCase);
    }

    public const string SyncedFolderWarning =
        "This file is in a folder that is synchronised between computers. Use it from one computer at a time: " +
        "if two computers edit their own copies of a task file at once, the sync service keeps one and the other's changes are lost.";

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir)) File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        foreach (var subDir in Directory.GetDirectories(sourceDir)) CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
    }

    private static void MoveFolder(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        if (Directory.Exists(destDir)) { CopyDirectory(sourceDir, destDir); Directory.Delete(sourceDir, recursive: true); }
        else Directory.Move(sourceDir, destDir);
    }

    // Windows' own "delete to Recycle Bin" (SHFileOperation with FOF_ALLOWUNDO).
    private static void Recycle(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = Path.GetFullPath(path) + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
        };
        var result = SHFileOperation(ref op);
        if (result != 0 || op.fAnyOperationsAborted) throw new IOException($"Windows could not send \"{path}\" to the Recycle Bin (error {result}).");
    }

    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004, FOF_NOCONFIRMATION = 0x0010, FOF_ALLOWUNDO = 0x0040, FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
