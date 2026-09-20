namespace KanbanApp.ViewModels;

// Backs the Settings dialog's Cancel button. Settings writes each change through the moment it's
// made, so cancelling means putting back what was there when the dialog opened: the dialog takes a
// snapshot on the way in and hands it back to RestoreSettings on Cancel.
//
// Covers every preference on the dialog. Deliberately not covered, because they are actions rather
// than preferences and can't sensibly be undone: Back Up Now, moving or switching the task file
// (which restarts the app anyway), and tidying the recent-files list.
public partial class MainViewModel
{
    // A record, so two snapshots compare by value - that's how the dialog tells whether anything
    // changed. Column names are flattened to one string for the same reason.
    public sealed record SettingsSnapshot(
        string ColumnDisplayNames,
        bool IsButtonsOnRight, int ColumnWidth,
        bool AutoBackupEnabled, int BackupRetentionCount,
        bool ShowSplash, int SplashDelayMs,
        string DefaultExportPath, string DefaultImportPath, string LinkedFilesDefaultPath,
        string UserName, string UserTitle, string UserEmail, string UserPhone,
        bool StartFullScreen, bool ConfirmDelete, bool ConfirmArchive, bool AddNoteOnComplete,
        bool ShowDueReminders, bool ShowTimeAlerts, bool RememberLastView, bool ShowWhatsNew, bool QuickAddHotkeyEnabled);

    private const char ColumnNameSeparator = ''; // can't be typed into a name

    public SettingsSnapshot CaptureSettings() => new(
        string.Join(ColumnNameSeparator, Columns.Select(c => c.DisplayName)),
        IsButtonsOnRight, ColumnWidth,
        AutoBackupEnabled, BackupRetentionCount,
        ShowSplash, SplashDelayMs,
        DefaultExportPath, DefaultImportPath, LinkedFilesDefaultPath,
        UserName, UserTitle, UserEmail, UserPhone,
        StartFullScreen, ConfirmDelete, ConfirmArchive, AddNoteOnComplete,
        ShowDueReminders, ShowTimeAlerts, RememberLastView, ShowWhatsNew, QuickAddHotkeyEnabled);

    // Goes through the same setters the dialog uses, so each value is saved and the board reacts
    // (column width, button side, column headings) exactly as if the user had typed it back.
    public void RestoreSettings(SettingsSnapshot snapshot)
    {
        var names = snapshot.ColumnDisplayNames.Split(ColumnNameSeparator);
        for (var i = 0; i < Columns.Count && i < names.Length; i++) RenameColumnDisplayName(Columns[i], names[i]);

        if (IsButtonsOnRight != snapshot.IsButtonsOnRight) ToggleButtonPosition();
        SetColumnWidth(snapshot.ColumnWidth);
        SetAutoBackupEnabled(snapshot.AutoBackupEnabled);
        SetBackupRetentionCount(snapshot.BackupRetentionCount);
        SetShowSplash(snapshot.ShowSplash);
        SetSplashDelayMs(snapshot.SplashDelayMs);
        SetDefaultExportPath(snapshot.DefaultExportPath);
        SetDefaultImportPath(snapshot.DefaultImportPath);
        SetLinkedFilesDefaultPath(snapshot.LinkedFilesDefaultPath);
        SetUserName(snapshot.UserName);
        SetUserTitle(snapshot.UserTitle);
        SetUserEmail(snapshot.UserEmail);
        SetUserPhone(snapshot.UserPhone);
        SetStartFullScreen(snapshot.StartFullScreen);
        SetConfirmDelete(snapshot.ConfirmDelete);
        SetConfirmArchive(snapshot.ConfirmArchive);
        SetAddNoteOnComplete(snapshot.AddNoteOnComplete);
        SetShowDueReminders(snapshot.ShowDueReminders);
        SetShowTimeAlerts(snapshot.ShowTimeAlerts);
        SetRememberLastView(snapshot.RememberLastView);
        SetShowWhatsNew(snapshot.ShowWhatsNew);
        SetQuickAddHotkeyEnabled(snapshot.QuickAddHotkeyEnabled);
    }
}
