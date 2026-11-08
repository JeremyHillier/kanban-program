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
        bool IsButtonsOnRight, int ColumnWidth, int MinFittedColumnWidth,
        bool AutoBackupEnabled, int BackupRetentionCount,
        bool ShowSplash, int SplashDelayMs,
        string DefaultExportPath, string DefaultImportPath, string LinkedFilesDefaultPath,
        string UserName, string UserTitle, string UserEmail, string UserPhone,
        bool StartWithWindows, bool StartFullScreen, bool ConfirmDelete, bool ConfirmArchive, bool AddNoteOnComplete,
        bool ShowDueReminders, bool ShowTimeAlerts, bool RememberLastView, bool ShowWhatsNew, bool QuickAddHotkeyEnabled, bool CheckForUpdatesEnabled, bool IsCompactButtons, bool IsFitColumnsToWindow,
        bool AutoCorrectEnabled, string AutoCorrectEntries, bool NotifySenderOnDone, string CustomDictionary, string HiddenCardParts, bool WeekStartsOnSunday);

    private const char ColumnNameSeparator = ''; // can't be typed into a name

    public SettingsSnapshot CaptureSettings() => new(
        string.Join(ColumnNameSeparator, Columns.Select(c => $"{c.Id}={c.DisplayName}")),
        IsButtonsOnRight, ColumnWidth, MinFittedColumnWidth,
        AutoBackupEnabled, BackupRetentionCount,
        ShowSplash, SplashDelayMs,
        DefaultExportPath, DefaultImportPath, LinkedFilesDefaultPath,
        UserName, UserTitle, UserEmail, UserPhone,
        StartWithWindows, StartFullScreen, ConfirmDelete, ConfirmArchive, AddNoteOnComplete,
        ShowDueReminders, ShowTimeAlerts, RememberLastView, ShowWhatsNew, QuickAddHotkeyEnabled, CheckForUpdatesEnabled, IsCompactButtons, IsFitColumnsToWindow,
        AutoCorrectEnabled, AutoCorrectEntries, NotifySenderOnDone, CustomDictionaryText, HiddenCardParts, WeekStartsOnSunday);

    // The labels in a snapshot, by column id: a column added or removed since is not in it, and the labels are put
    // back on the columns they belong to, whatever their place now.
    private static Dictionary<int, string> ColumnNamesIn(SettingsSnapshot snapshot)
    {
        var names = new Dictionary<int, string>();
        foreach (var pair in snapshot.ColumnDisplayNames.Split(ColumnNameSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var at = pair.IndexOf('=');
            if (at > 0 && int.TryParse(pair[..at], out var id)) names[id] = pair[(at + 1)..];
        }
        return names;
    }

    // Adding, moving and removing columns take effect at once and Cancel does not undo them, so a snapshot taken
    // earlier is brought up to date with the columns there are now (keeping the labels it remembers for the ones
    // still here), and Cancel then asks only about what it can put back.
    public SettingsSnapshot WithCurrentColumns(SettingsSnapshot snapshot)
    {
        var known = ColumnNamesIn(snapshot);
        return snapshot with { ColumnDisplayNames = string.Join(ColumnNameSeparator, Columns.Select(c => $"{c.Id}={known.GetValueOrDefault(c.Id, c.DisplayName)}")) };
    }

    // Goes through the same setters the dialog uses, so each value is saved and the board reacts
    // (column width, button side, column headings) exactly as if the user had typed it back.
    public void RestoreSettings(SettingsSnapshot snapshot)
    {
        foreach (var (id, label) in ColumnNamesIn(snapshot))
        {
            if (Columns.FirstOrDefault(c => c.Id == id) is { } column) RenameColumnDisplayName(column, label);
        }

        if (IsButtonsOnRight != snapshot.IsButtonsOnRight) ToggleButtonPosition();
        SetColumnWidth(snapshot.ColumnWidth);
        SetMinFittedColumnWidth(snapshot.MinFittedColumnWidth);
        SetFitColumnsToWindow(snapshot.IsFitColumnsToWindow);
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
        SetStartWithWindows(snapshot.StartWithWindows);
        SetStartFullScreen(snapshot.StartFullScreen);
        SetConfirmDelete(snapshot.ConfirmDelete);
        SetConfirmArchive(snapshot.ConfirmArchive);
        SetAddNoteOnComplete(snapshot.AddNoteOnComplete);
        SetShowDueReminders(snapshot.ShowDueReminders);
        SetShowTimeAlerts(snapshot.ShowTimeAlerts);
        SetRememberLastView(snapshot.RememberLastView);
        SetShowWhatsNew(snapshot.ShowWhatsNew);
        SetQuickAddHotkeyEnabled(snapshot.QuickAddHotkeyEnabled);
        SetCheckForUpdatesEnabled(snapshot.CheckForUpdatesEnabled);
        SetCompactButtons(snapshot.IsCompactButtons);
        SetAutoCorrectEnabled(snapshot.AutoCorrectEnabled);
        SetAutoCorrectEntries(snapshot.AutoCorrectEntries);
        SetNotifySenderOnDone(snapshot.NotifySenderOnDone);
        SetCustomDictionary(snapshot.CustomDictionary);
        SetHiddenCardParts(snapshot.HiddenCardParts);
        SetWeekStartsOnSunday(snapshot.WeekStartsOnSunday);
    }
}
