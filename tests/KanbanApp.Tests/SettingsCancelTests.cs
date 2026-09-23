using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// The Settings dialog saves as you go, so its Cancel button works by restoring a snapshot.
[Collection(WpfCollection.Name)]
public sealed class SettingsCancelTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainViewModel OpenBoard() => new(new DatabaseService(_temp.File("board.db")));

    private static void ChangeEverything(MainViewModel board)
    {
        board.RenameColumnDisplayName(board.Columns[0], "Backlog");
        board.ToggleButtonPosition();
        board.SetColumnWidth(board.ColumnWidth + 40);
        board.SetFitColumnsToWindow(!board.IsFitColumnsToWindow);
        board.SetAutoBackupEnabled(!board.AutoBackupEnabled);
        board.SetBackupRetentionCount(board.BackupRetentionCount + 3);
        board.SetShowSplash(!board.ShowSplash);
        board.SetSplashDelayMs(board.SplashDelayMs + 500);
        board.SetDefaultExportPath(@"C:\Exports");
        board.SetDefaultImportPath(@"C:\Imports");
        board.SetLinkedFilesDefaultPath(@"C:\Files");
        board.SetUserName("Jane Doe");
        board.SetUserTitle("Manager");
        board.SetUserEmail("jane@example.com");
        board.SetUserPhone("555-0100");
        board.SetStartFullScreen(!board.StartFullScreen);
        board.SetConfirmDelete(!board.ConfirmDelete);
        board.SetConfirmArchive(!board.ConfirmArchive);
        board.SetAddNoteOnComplete(!board.AddNoteOnComplete);
        board.SetShowDueReminders(!board.ShowDueReminders);
        board.SetShowTimeAlerts(!board.ShowTimeAlerts);
        board.SetRememberLastView(!board.RememberLastView);
        board.SetShowWhatsNew(!board.ShowWhatsNew);
        board.SetQuickAddHotkeyEnabled(!board.QuickAddHotkeyEnabled);
        board.SetCheckForUpdatesEnabled(!board.CheckForUpdatesEnabled);
        board.SetCompactButtons(!board.IsCompactButtons);
    }

    [Fact]
    public void ASnapshotNoticesEverySettingChanging() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var before = board.CaptureSettings();
        Assert.Equal(before, board.CaptureSettings());

        ChangeEverything(board);
        var after = board.CaptureSettings();

        // Every field differs, so no setting on the dialog is missing from the snapshot's compare.
        foreach (var property in typeof(MainViewModel.SettingsSnapshot).GetProperties())
        {
            Assert.NotEqual(property.GetValue(before), property.GetValue(after));
        }
    });

    [Fact]
    public void RestoringPutsEverythingBack_OnScreenAndInTheTaskFile() => wpf.Run(() =>
    {
        var board = OpenBoard();
        var before = board.CaptureSettings();

        ChangeEverything(board);
        board.RestoreSettings(before);

        Assert.Equal(before, board.CaptureSettings());
        Assert.Equal(before, OpenBoard().CaptureSettings());
    });

    [Fact]
    public void ChangesThatWereKept_AreStillThereNextTime() => wpf.Run(() =>
    {
        var board = OpenBoard();
        ChangeEverything(board);
        var kept = board.CaptureSettings();

        Assert.Equal(kept, OpenBoard().CaptureSettings());
    });
}
