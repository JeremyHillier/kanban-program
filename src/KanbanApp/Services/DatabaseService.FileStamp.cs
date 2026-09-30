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
    // The newest format this copy of the app understands.
    public const int CurrentFileFormat = 4;

    // What every file is raised to just by being opened. Format 4 adds no table or column: a file
    // whose priorities are still the standard four is no different from a format 3 file, so it is
    // only stamped 4 once the list is actually changed (RaiseFileFormat). That keeps older copies
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
