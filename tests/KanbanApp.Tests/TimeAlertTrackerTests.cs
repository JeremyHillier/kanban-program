using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

public sealed class TimeAlertTrackerTests
{
    private static readonly DateTime Nine = new(2026, 9, 15, 9, 0, 0);

    private static CardViewModel Card(int id, DateTime? dueAt) => new(new CardItem
    {
        Id = id,
        Title = $"Task {id}",
        DueDate = dueAt?.Date,
        DueTime = dueAt?.ToString("HH:mm")
    });

    [Fact]
    public void ATaskReachingItsTime_AlertsOnce()
    {
        var tracker = new TimeAlertTracker();
        var card = Card(1, Nine);

        Assert.Equal([card], tracker.TakeCardsToAlert([card], Nine.AddSeconds(10)));
        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddMinutes(1)));
        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddHours(3)));
    }

    [Fact]
    public void TasksAlreadyPastAtStartup_DoNotAlert()
    {
        var tracker = new TimeAlertTracker();
        var card = Card(1, Nine);
        tracker.MarkAnnounced([card]);

        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddMinutes(5)));
    }

    [Fact]
    public void ASnoozedTask_AlertsAgainWhenTheSnoozeRunsOut_AndOnlyOnce()
    {
        var tracker = new TimeAlertTracker();
        var card = Card(1, Nine);
        tracker.TakeCardsToAlert([card], Nine);

        tracker.Snooze([card], Nine.AddMinutes(15));

        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddMinutes(14)));
        Assert.Equal([card], tracker.TakeCardsToAlert([card], Nine.AddMinutes(15)));
        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddMinutes(16)));
    }

    [Fact]
    public void ATaskCanBeSnoozedRepeatedly()
    {
        var tracker = new TimeAlertTracker();
        var card = Card(1, Nine);
        tracker.TakeCardsToAlert([card], Nine);

        tracker.Snooze([card], Nine.AddHours(1));
        Assert.Equal([card], tracker.TakeCardsToAlert([card], Nine.AddHours(1)));

        tracker.Snooze([card], Nine.AddHours(3));
        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddHours(2)));
        Assert.Equal([card], tracker.TakeCardsToAlert([card], Nine.AddHours(3)));
    }

    [Fact]
    public void SnoozingOneTask_LeavesOthersAlone()
    {
        var tracker = new TimeAlertTracker();
        var snoozed = Card(1, Nine);
        var closed = Card(2, Nine);
        tracker.TakeCardsToAlert([snoozed, closed], Nine);

        tracker.Snooze([snoozed], Nine.AddMinutes(15));

        Assert.Equal([snoozed], tracker.TakeCardsToAlert([snoozed, closed], Nine.AddMinutes(20)));
    }

    [Fact]
    public void ASnoozedTaskThatIsCompleted_NeverAlertsAgain()
    {
        var tracker = new TimeAlertTracker();
        var card = Card(1, Nine);
        tracker.TakeCardsToAlert([card], Nine);
        tracker.Snooze([card], Nine.AddMinutes(15));

        tracker.TakeCardsToAlert([], Nine.AddMinutes(5)); // marked Done: no longer past due

        Assert.Empty(tracker.TakeCardsToAlert([], Nine.AddMinutes(20)));
        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddMinutes(30))); // even if reopened later
    }

    [Fact]
    public void RescheduledLater_AlertsAtTheNewTimeAndDropsTheOldSnooze()
    {
        var tracker = new TimeAlertTracker();
        var card = Card(1, Nine);
        tracker.TakeCardsToAlert([card], Nine);
        tracker.Snooze([card], Nine.AddMinutes(15));

        // Moved to 11:00: until then it isn't past due at all.
        card.DueTime = "11:00";
        Assert.Empty(tracker.TakeCardsToAlert([], Nine.AddMinutes(20)));

        Assert.Equal([card], tracker.TakeCardsToAlert([card], Nine.AddHours(2)));
        Assert.Empty(tracker.TakeCardsToAlert([card], Nine.AddHours(2).AddMinutes(1)));
    }

    [Fact]
    public void TasksWithoutATime_AreIgnored()
    {
        var tracker = new TimeAlertTracker();
        var undated = Card(1, null);

        Assert.Empty(tracker.TakeCardsToAlert([undated], Nine));
        tracker.Snooze([undated], Nine.AddMinutes(15));
        Assert.Empty(tracker.TakeCardsToAlert([undated], Nine.AddHours(1)));
    }

    [Fact]
    public void SnoozeOptions_MatchWhatTheMenuOffers() =>
        Assert.Equal(
            [("15 minutes", TimeSpan.FromMinutes(15)), ("1 hour", TimeSpan.FromHours(1)), ("2 hours", TimeSpan.FromHours(2)),
             ("4 hours", TimeSpan.FromHours(4)), ("1 day", TimeSpan.FromDays(1))],
            TimeAlertTracker.SnoozeOptions);
}
