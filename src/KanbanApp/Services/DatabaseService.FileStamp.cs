using System.Globalization;
using System.Reflection;

namespace KanbanApp.Services;

// The task file records the newest "file format" that has ever saved it, so an older copy of the
// app can tell it is looking at a file a newer copy has used - an old app doesn't know about newer
// fields (Start date, Waiting On, ...) and can lose them on the tasks it edits.
//
// CurrentFileFormat goes up by one whenever what is stored changes shape: a new table, a new
// column, or a new meaning for an old one. It is NOT the app version - most releases don't touch
// the file, and warning about those would only teach people to ignore the warning.
// FileFormatTests fails when the tables change without this number changing.
public partial class DatabaseService
{
    // 1: first stamped format (0.102.0). 2: CardPeople - a task can have several people (0.103.0).
    // 3: Cards.RecurrencesLeft - a recurring task can stop after a number of times (0.111.0).
    // 4: the priority list can be changed, so a task's priority can be a name older copies don't
    //    offer - they would put it back to Normal on any task they edit (0.119.0).
    // 5: Cards.ShareId - a task shared by email keeps one identity, so importing it again updates
    //    it (0.127.0). Older copies leave the ID alone, but would import a returned task as a second
    //    copy, so the file is only stamped 5 once a task in it has an ID (RaiseFileFormat).
    // 6: "Days of each month" repeating (0.137.0) - a new meaning for Cards.RecurrencePattern that
    //    older copies would repeat daily for ever, so the file is only stamped 6 once such a task is
    //    saved (DayRangeFileFormat). No table changed.
    // 7: a task's link can be a file or folder (0.138.0) - older copies refuse to save such a link, so
    //    the file is only stamped 7 once one is saved (PathLinkFileFormat). No table changed.
    // 8: SharedTasks - who sent a shared task, and what it looked like when last exchanged, so a returned
    //    copy can be told from a clash (0.139.0). A new table older copies ignore, stamped on first write.
    // 9: Goals.TargetDate (0.148.0) - older copies ignore the column but would lose it if they rewrote the
    //    list, so the file is only stamped 9 once a target date has been set (GoalTargetFileFormat).
    // 10: GoalMilestones (0.150.0) - a goal's dated milestones, in a table older copies ignore; stamped on the
    //     first milestone saved (GoalMilestoneFileFormat).
    // 11: Goals.StartDate (0.151.0) - older copies ignore the column; stamped when a start date is first set (GoalStartFileFormat).
    // 12: Projects.StartDate / TargetDate and ProjectMilestones (0.153.0) - stamped when one is first saved (ProjectPlanFileFormat).
    // 13: "Every 2 years" repeating (0.157.0) - a new meaning for Cards.RecurrencePattern that older copies
    //     would repeat daily for ever, so the file is only stamped 13 once such a task is saved
    //     (RecurrenceIntervalFileFormat). No table changed.
    // 14: CardRichNotes (0.158.0) - the formatting of a task's Notes beside the plain text; stamped when formatting is first saved (RichNotesFileFormat).
    // The newest format this copy of the app understands.
    public const int CurrentFileFormat = 14;

    public const int DayRangeFileFormat = 6;
    public const int PathLinkFileFormat = 7;
    public const int GoalTargetFileFormat = 9;
    public const int GoalMilestoneFileFormat = 10;
    public const int GoalStartFileFormat = 11;
    public const int ProjectPlanFileFormat = 12;
    public const int RichNotesFileFormat = 14;
    public const int RecurrenceIntervalFileFormat = 13;

    // What every file is raised to just by being opened. Formats 4 to 8 only matter once their
    // feature is used: a file whose priorities are still the standard four, and with no shared
    // task, is no different from a format 3 file. So 4 is stamped when the priority list is first
    // changed and 5 when a task first gets a share ID (RaiseFileFormat). That keeps older copies
    // from warning about files they can still handle perfectly well.
    public const int FormatStampedOnOpen = 3;

    private const string FileFormatKey = "FileFormat";
    private const string FileFormatAppVersionKey = "FileFormatAppVersion";
    private const string LastOpenedByVersionKey = "LastOpenedByVersion";

    internal static string RunningAppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";

    // The format the file claimed when this session opened it; 0 for a file from before the stamp.
    public int FileFormatAtOpen { get; private set; }

    // True when a newer copy of the app, one that stores things this copy doesn't know about, has
    // used this file.
    public bool IsFromNewerApp => FileFormatAtOpen > CurrentFileFormat;

    // The app version that raised the file to its format - what to tell the user to update to.
    public string? NewerAppVersion => IsFromNewerApp ? GetSetting(FileFormatAppVersionKey) : null;

    // The stamp only ever goes up: an older app opening a newer file must leave the higher number
    // in place, or the warning would show once and never again.
    private void StampFile()
    {
        FileFormatAtOpen = int.TryParse(GetSetting(FileFormatKey), NumberStyles.None, CultureInfo.InvariantCulture, out var stored) ? stored : 0;

        if (FileFormatAtOpen < FormatStampedOnOpen)
        {
            SetSetting(FileFormatKey, FormatStampedOnOpen.ToString(CultureInfo.InvariantCulture));
            SetSetting(FileFormatAppVersionKey, RunningAppVersion);
        }

        if (GetSetting(LastOpenedByVersionKey) != RunningAppVersion) SetSetting(LastOpenedByVersionKey, RunningAppVersion);
    }

    // For a format that only applies once a feature is used (see FormatStampedOnOpen): raises the
    // stamp to it, if it isn't there already. Like StampFile, it never lowers the stamp.
    public void RaiseFileFormat(int format)
    {
        var stored = int.TryParse(GetSetting(FileFormatKey), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
        if (stored >= format) return;

        SetSetting(FileFormatKey, format.ToString(CultureInfo.InvariantCulture));
        SetSetting(FileFormatAppVersionKey, RunningAppVersion);
    }
}
