using KanbanApp.Models;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The list behind Archived Tasks and Deleted Tasks: the From/To date filter and taking tasks out.
public sealed class RecoverableTaskListTests
{
    [Theory]
    [InlineData("2026-09-10 08:00:00", null, null, true)]         // no dates: everything
    [InlineData("2026-09-10 08:00:00", "2026-09-10", null, true)] // the From day itself counts
    [InlineData("2026-09-09 23:59:00", "2026-09-10", null, false)]
    [InlineData("2026-09-20 23:59:00", null, "2026-09-20", true)] // the To day itself counts, all of it
    [InlineData("2026-09-21 00:00:00", null, "2026-09-20", false)]
    [InlineData("2026-09-15 12:00:00", "2026-09-10", "2026-09-20", true)]
    [InlineData("not a date", "2026-09-10", "2026-09-20", true)]  // unreadable dates are never hidden
    [InlineData("", "2026-09-10", null, true)]
    public void TheDateRange_KeepsBothEnds_AndNeverHidesAnUnreadableDate(string stamp, string? from, string? to, bool shown)
    {
        DateTime? D(string? s) => s is null ? null : DateTime.Parse(s);
        Assert.Equal(shown, RecoverableTaskList<ArchivedCardInfo>.MatchesDateRange(stamp, D(from), D(to)));
    }

    [Fact]
    public void Filtering_ShowsTheMatchingTasks_AndClearingShowsThemAllAgain()
    {
        var early = new ArchivedCardInfo { Id = 1, Title = "Early", ArchivedAt = "2026-08-01 09:00:00" };
        var late = new ArchivedCardInfo { Id = 2, Title = "Late", ArchivedAt = "2026-09-15 09:00:00" };
        var list = new RecoverableTaskList<ArchivedCardInfo>([early, late], i => i.ArchivedAt);
        Assert.Equal([early, late], list.Shown);

        list.Filter(new DateTime(2026, 9, 1), null);
        Assert.Equal([late], list.Shown);

        list.Filter(null, null);
        Assert.Equal([early, late], list.Shown);
    }

    [Fact]
    public void ATaskTakenOut_StaysOut_EvenWhenTheFilterChanges()
    {
        var a = new DeletedCardInfo { Id = 1, Title = "A", DeletedAt = "2026-09-01 09:00:00" };
        var b = new DeletedCardInfo { Id = 2, Title = "B", DeletedAt = "2026-09-02 09:00:00" };
        var list = new RecoverableTaskList<DeletedCardInfo>([a, b], i => i.DeletedAt);

        list.Remove(a);
        list.Filter(null, null);

        Assert.Equal([b], list.Shown);
        list.Remove(b);
        Assert.True(list.IsEmpty);
    }
}
