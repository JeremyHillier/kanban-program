using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Settings > Large cards show: which details and buttons a large card shows.
public sealed class CardPartsShownTests
{
    [Fact]
    public void EverythingShows_UntilSwitchedOff()
    {
        var parts = CardPartsShown.All;
        Assert.All(Enum.GetValues<CardPart>(), part => Assert.True(parts.Shows(part)));
        Assert.Equal(string.Empty, parts.Serialize());
    }

    [Fact]
    public void SwitchingOff_SavesOnlyWhatIsOff_InAFixedOrder()
    {
        var parts = CardPartsShown.All.With(CardPart.Updated, false).With(CardPart.Goal, false).With(CardPart.EmailButton, false);

        Assert.False(parts.Goal);
        Assert.False(parts.Updated);
        Assert.False(parts.EmailButton);
        Assert.True(parts.DueDate);
        Assert.Equal("Goal,Updated,EmailButton", parts.Serialize());

        Assert.True(parts.With(CardPart.Goal, true).Goal);
    }

    [Fact]
    public void ReadingBack_IgnoresAnythingItDoesNotKnow()
    {
        var parts = CardPartsShown.Parse(" Goal , NoSuchPart,,SubTasks ");

        Assert.False(parts.Goal);
        Assert.False(parts.SubTasks);
        Assert.Equal("Goal,SubTasks", parts.Serialize());
        Assert.Equal(string.Empty, CardPartsShown.Parse(null).Serialize());
    }

    [Fact]
    public void EveryPart_HasALabelForSettings() =>
        Assert.All(Enum.GetValues<CardPart>(), part => Assert.False(string.IsNullOrWhiteSpace(CardPartsShown.Label(part))));

    // Every part has a switch on the card itself: a binding to CardParts.<Part> in the card template.
    [Fact]
    public void EveryPart_IsBoundOnTheCard()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "KanbanApp.slnx"))) dir = dir.Parent;
        var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, "src", "KanbanApp", "MainWindow.xaml"));

        Assert.All(Enum.GetValues<CardPart>(), part => Assert.Contains($"DataContext.CardParts.{part},", xaml));
    }
}

[Collection(WpfCollection.Name)]
public sealed class CardPartsSettingTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheChoice_IsKeptInTheTaskFile() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        board.SetCardPartShown(CardPart.Repeats, false);
        board.SetCardPartShown(CardPart.CalendarButton, false);

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.False(reopened.CardParts.Repeats);
        Assert.False(reopened.CardParts.CalendarButton);
        Assert.True(reopened.CardParts.Goal);
    });
}
