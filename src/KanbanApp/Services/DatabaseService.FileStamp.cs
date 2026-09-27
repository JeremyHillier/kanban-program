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
    public const int CurrentFileFormat = 3;

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

        if (FileFormatAtOpen < CurrentFileFormat)
        {
            SetSetting(FileFormatKey, CurrentFileFormat.ToString(CultureInfo.InvariantCulture));
            SetSetting(FileFormatAppVersionKey, RunningAppVersion);
        }

        if (GetSetting(LastOpenedByVersionKey) != RunningAppVersion) SetSetting(LastOpenedByVersionKey, RunningAppVersion);
    }
}
