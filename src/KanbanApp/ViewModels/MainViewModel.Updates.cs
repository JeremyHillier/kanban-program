using System.Globalization;
using System.Text.Json;
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
        _db.SetFlag("CheckForUpdates", value);
        OnPropertyChanged(nameof(CheckForUpdatesEnabled));
    }

    private void LoadUpdateSettings()
    {
        CheckForUpdatesEnabled = _db.GetFlag("CheckForUpdates", true);
        AvailableUpdate = LoadAvailableUpdate();
    }

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

    // Skipping the version the Update button is for takes the button away, whichever window asked.
    public void SkipUpdateVersion(string version)
    {
        _db.SetSetting("SkippedUpdateVersion", version);
        if (AvailableUpdate is { } offered && string.Equals(offered.Version, version, StringComparison.OrdinalIgnoreCase)) NoteUpdateCheck(offered);
    }

    // The automatic check stays quiet about a version the user chose to skip; a newer one after
    // that is offered again. Checking by hand from About ignores the skip.
    // The newer version either check has found and that is still worth offering, for as long as the
    // app runs: About shows a Get Update button while this is set. A version the user chose to skip
    // is not kept by the automatic check, so it stays quiet; checking by hand from About finds it again.
    public UpdateInfo? AvailableUpdate { get; private set; }
    public bool HasAvailableUpdate => AvailableUpdate is not null;

    // Kept in the task file, per PC like the last-check time, so the Update button is still there after a
    // restart: it stays until this copy has been updated past that version, or the version is skipped.
    private string AvailableUpdateKey => $"AvailableUpdate:{ThisComputer}";

    private UpdateInfo? LoadAvailableUpdate()
    {
        var json = _db.GetSetting(AvailableUpdateKey);
        if (string.IsNullOrEmpty(json)) return null;

        UpdateInfo? stored;
        try { stored = JsonSerializer.Deserialize<UpdateInfo>(json); }
        catch (JsonException) { stored = null; }

        if (ShouldOfferUpdate(stored)) return stored;
        _db.SetSetting(AvailableUpdateKey, string.Empty); // updated since, or skipped: nothing to offer now
        return null;
    }

    // Called with what a check found. A check that could not reach the list (null) says nothing
    // new, so it leaves what is known alone; one that found nothing newer clears it.
    public void NoteUpdateCheck(UpdateInfo? update, bool askedByHand = false)
    {
        if (update is null) return;
        AvailableUpdate = ShouldOfferUpdate(update, askedByHand) ? update : null;
        _db.SetSetting(AvailableUpdateKey, AvailableUpdate is null ? string.Empty : JsonSerializer.Serialize(AvailableUpdate));
        OnPropertyChanged(nameof(AvailableUpdate));
        OnPropertyChanged(nameof(HasAvailableUpdate));
    }

    public bool ShouldOfferUpdate(UpdateInfo? update, bool askedByHand = false) =>
        update is not null
        && UpdateChecker.IsNewer(update.Version, AppVersion)
        && (askedByHand || !string.Equals(update.Version, SkippedUpdateVersion, StringComparison.OrdinalIgnoreCase));
}
