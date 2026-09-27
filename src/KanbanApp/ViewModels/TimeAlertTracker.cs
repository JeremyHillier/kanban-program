namespace KanbanApp.ViewModels;

// Decides which tasks the due-time alert should announce on each timer tick. Session-only: nothing
// here survives a restart, so times that pass (or snoozes that would end) while the app is closed
// are left to the startup reminder list instead.
//
// Everything is keyed on the card and its due moment together, so rescheduling a task gives it a
// fresh key: moving the time later alerts again at the new time, and any snooze on the old time
// simply lapses, with no hook into the edit path needed.
public sealed class TimeAlertTracker
{
    private readonly HashSet<(int CardId, DateTime DueAt)> _announced = [];
    private readonly Dictionary<(int CardId, DateTime DueAt), DateTime> _snoozedUntil = [];

    public static readonly IReadOnlyList<(string Label, TimeSpan Duration)> SnoozeOptions =
    [
        ("15 minutes", TimeSpan.FromMinutes(15)),
        ("1 hour", TimeSpan.FromHours(1)),
        ("2 hours", TimeSpan.FromHours(2)),
        ("4 hours", TimeSpan.FromHours(4)),
        ("1 day", TimeSpan.FromDays(1))
    ];

    // Call once at startup with everything already past, so the first tick doesn't pop them all.
    public void MarkAnnounced(IEnumerable<CardViewModel> cards)
    {
        foreach (var card in cards)
        {
            if (card.DueDateTime is { } dueAt) _announced.Add((card.Id, dueAt));
        }
    }

    // pastDue: every task whose due moment has passed and isn't Done. Returns the ones reaching their
    // due time for the first time, plus any whose snooze has run out, and records them as announced.
    public List<CardViewModel> TakeCardsToAlert(IReadOnlyCollection<CardViewModel> pastDue, DateTime now)
    {
        var toAlert = new List<CardViewModel>();
        var stillPastDue = new HashSet<(int, DateTime)>();

        foreach (var card in pastDue)
        {
            if (card.DueDateTime is not { } dueAt) continue;
            var key = (card.Id, dueAt);
            stillPastDue.Add(key);

            if (_announced.Add(key))
            {
                toAlert.Add(card);
            }
            else if (_snoozedUntil.TryGetValue(key, out var wakeAt) && wakeAt <= now)
            {
                _snoozedUntil.Remove(key);
                toAlert.Add(card);
            }
        }

        // A snoozed task that's since been completed, deleted, or rescheduled can never come due on
        // its old key, so drop it rather than let the list grow.
        foreach (var stale in _snoozedUntil.Keys.Where(key => !stillPastDue.Contains(key)).ToList())
        {
            _snoozedUntil.Remove(stale);
        }

        return toAlert;
    }

    public void Snooze(IEnumerable<CardViewModel> cards, DateTime until)
    {
        foreach (var card in cards)
        {
            if (card.DueDateTime is { } dueAt) _snoozedUntil[(card.Id, dueAt)] = until;
        }
    }
}
