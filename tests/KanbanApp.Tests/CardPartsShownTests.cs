using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Tests;

// Settings > Large cards show and Card buttons: which details a large card shows and which buttons a card has.
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

    [Fact]
    public void EveryPart_IsEitherADetailOrAButton_NeverBoth()
    {
        Assert.Equal(Enum.GetValues<CardPart>().Order(), CardPartsShown.DetailParts.Concat(CardPartsShown.ButtonParts).Order());
        Assert.Empty(CardPartsShown.DetailParts.Intersect(CardPartsShown.ButtonParts));
        Assert.Equal(9, CardPartsShown.ButtonParts.Count);
        Assert.Equal(10, CardPartsShown.DetailParts.Count());
    }

    [Fact]
    public void TheFootRow_IsLeftOut_OnlyWhenNoneOfItsButtonsIsTicked()
    {
        var parts = CardPartsShown.All;
        Assert.True(parts.AnyFootButton);
        Assert.True(parts.With(CardPart.MoveWaitingButton, false).AnyFootButton);

        var noMoves = parts.With(CardPart.MoveToDoButton, false).With(CardPart.MoveInProgressButton, false)
            .With(CardPart.MoveOnHoldButton, false).With(CardPart.MoveWaitingButton, false);
        Assert.True(noMoves.AnyFootButton);

        var none = noMoves.With(CardPart.DoneButton, false).With(CardPart.DeleteButton, false);
        Assert.False(none.AnyFootButton);
        Assert.True(none.FlagButton);   // the side buttons have their own say
    }

    [Fact]
    public void AnEarlierChoice_StillReads_AndTheNewButtonsShowUntilTurnedOff()
    {
        var parts = CardPartsShown.Parse("Goal,EmailButton");

        Assert.True(parts.MoveToDoButton && parts.DoneButton && parts.DeleteButton);
        Assert.Equal("Goal,EmailButton", parts.Serialize());
        Assert.Equal("EmailButton,DeleteButton", CardPartsShown.All.With(CardPart.DeleteButton, false).With(CardPart.EmailButton, false).Serialize());
    }

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

    [Fact]
    public void TheFootRow_ShowsWhileAnyOfItsButtonsIsTicked_AndHideButtonsStillTakesEveryButtonOff() => wpf.Run(() =>
    {
        var board = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        var changed = new List<string?>();
        board.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        CardPart[] foot = [CardPart.MoveToDoButton, CardPart.MoveInProgressButton, CardPart.MoveOnHoldButton, CardPart.MoveWaitingButton, CardPart.DoneButton, CardPart.DeleteButton];

        Assert.True(board.ShowCardFootButtons);
        foreach (var part in foot) board.SetCardPartShown(part, false);
        Assert.False(board.ShowCardFootButtons);
        Assert.Contains(nameof(MainViewModel.ShowCardFootButtons), changed);

        board.SetCardPartShown(CardPart.DeleteButton, true);
        Assert.True(board.ShowCardFootButtons);

        board.ToggleCardButtons();                  // Hide Buttons
        Assert.False(board.ShowCardFootButtons);
        board.ToggleCardButtons();                  // Show Buttons: only what is ticked comes back
        Assert.True(board.ShowCardFootButtons);

        var reopened = new MainViewModel(new DatabaseService(_temp.File("board.db")));
        Assert.False(reopened.CardParts.MoveToDoButton);
        Assert.True(reopened.CardParts.DeleteButton);
        Assert.True(reopened.ShowCardFootButtons);
    });
}
