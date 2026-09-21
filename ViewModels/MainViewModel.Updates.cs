using System.Globalization;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// The once-a-day check for a newer version (Services/UpdateChecker): whether it is switched on,
// when it last ran, and which version the user has said not to be told about again.
public partial class MainViewModel
{
    public bool CheckForUpdatesEnabled { get; private set; } = true;

    public void SetCheckForUpdatesEnabled(bool value)
    {
        if (CheckForUpdatesEnabled == value) return;
        CheckForUpdatesEnabled = value;
        _db.SetSetting("CheckForUpdates", value ? "True" : "False");
        OnPropertyChanged(nameof(CheckForUpdatesEnabled));
    }

    private void LoadUpdateSettings() => CheckForUpdatesEnabled = _db.GetSetting("CheckForUpdates") != "False";

    // When THIS PC last checked. Kept per computer, not per task file: two PCs can share one task
    // file (a synced folder), and with a shared timestamp whichever started first each day silenced
    // the other - so an out-of-date PC beside an up-to-date one was never told. The old shared
    // "LastUpdateCheck" setting is ignored for the same reason.


    private string LastUpdateCheckKey => $"LastUpdateCheck:{ThisComputer}";

    public DateTime? LastUpdateCheck =>
        DateTime.TryParse(_db.GetSetting(LastUpdateCheckKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when) ? when : null;

    public bool IsUpdateCheckDue(DateTime now) => CheckForUpdatesEnabled && UpdateChecker.IsDue(LastUpdateCheck, now);

    // Only the automatic check calls this. Checking by hand from About leaves the daily timer
    // alone, so trying the button never postpones the next automatic check.
    public void RecordUpdateCheck(DateTime now) => _db.SetSetting(LastUpdateCheckKey, now.ToString("o", CultureInfo.InvariantCulture));

    public string? SkippedUpdateVersion => _db.GetSetting("SkippedUpdateVersion") is { Length: > 0 } version ? version : null;

    public void SkipUpdateVersion(string version) => _db.SetSetting("SkippedUpdateVersion", version);

    // The automatic check stays quiet about a version the user chose to skip; a newer one after
    // that is offered again. Checking by hand from About ignores the skip.
    public bool ShouldOfferUpdate(UpdateInfo? update, bool askedByHand = false) =>
        update is not null
        && UpdateChecker.IsNewer(update.Version, AppVersion)
        && (askedByHand || !string.Equals(update.Version, SkippedUpdateVersion, StringComparison.OrdinalIgnoreCase));
}
