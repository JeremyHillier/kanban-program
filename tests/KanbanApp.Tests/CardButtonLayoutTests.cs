using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KanbanApp.Converters;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The buttons left on a card: as few even rows as fit, and the side buttons joining the foot row when they fit.
public sealed class CardButtonLayoutTests
{
    [Theory]
    [InlineData(6, 6, 4, new[] { 6 })]
    [InlineData(6, 5, 4, new[] { 4, 2 })]
    [InlineData(6, 4, 4, new[] { 4, 2 })]
    [InlineData(6, 3, 4, new[] { 3, 3 })]
    [InlineData(6, 2, 4, new[] { 2, 2, 2 })]
    [InlineData(5, 4, 4, new[] { 3, 2 })]   // four moves and Done: not 4 + 1
    [InlineData(5, 4, 3, new[] { 3, 2 })]   // three moves, then Done and Delete
    [InlineData(4, 3, 2, new[] { 2, 2 })]
    [InlineData(3, 2, -1, new[] { 2, 1 })]
    [InlineData(2, 0, 1, new[] { 1, 1 })]
    [InlineData(0, 4, -1, new int[0])]
    public void SplitRows_UsesFewRows_EvenlyFilled_KeepingTheGroupsApartWhenThatWorks(int count, int perRow, int groupStart, int[] expected) =>
        Assert.Equal(expected, CardButtonLayout.SplitRows(count, perRow, groupStart));

    [Theory]
    [InlineData(181, 2, 1, true)]     // Done, Delete and the calendar button
    [InlineData(181, 6, 2, false)]    // everything, in a standard column: 214 needed
    [InlineData(240, 6, 3, true)]
    [InlineData(181, 0, 2, false)]    // no foot row: the side buttons stay at the side
    [InlineData(181, 3, 0, false)]
    [InlineData(0, 2, 1, false)]      // not laid out yet
    public void SideButtons_JoinTheFootRow_OnlyWhenEverythingFitsOnOneRow(double width, int foot, int side, bool joins) =>
        Assert.Equal(joins, CardButtonLayout.SideButtonsJoinRow(width, foot, side));

    [Fact]
    public void TheCounts_FollowTheTicks_TheCardSize_AndWhetherThereIsSomeoneToEmail()
    {
        var all = CardPartsShown.All;

        Assert.Equal(6, CardButtonLayout.FootButtonCount(all));
        Assert.Equal(5, CardButtonLayout.FootButtonCount(all.With(CardPart.DeleteButton, false)));
        Assert.Equal(2, CardButtonLayout.SideButtonCount(all, isLargeCard: true, canEmail: false));
        Assert.Equal(3, CardButtonLayout.SideButtonCount(all, isLargeCard: true, canEmail: true));
        Assert.Equal(1, CardButtonLayout.SideButtonCount(all, isLargeCard: false, canEmail: true));
        Assert.Equal(1, CardButtonLayout.SideButtonCount(all.With(CardPart.FlagButton, false), isLargeCard: true, canEmail: false));
    }

    [Fact]
    public void TheConverter_SaysRow_OnlyWhenTheSideButtonsFit()
    {
        var converter = new CardButtonPlacementConverter();
        object Place(object width, CardPartsShown parts, bool showButtons = true) =>
            converter.Convert([width, parts, showButtons, true, false], typeof(object), null!, CultureInfo.InvariantCulture);

        var doneAndDelete = CardPartsShown.All.With(CardPart.MoveToDoButton, false).With(CardPart.MoveInProgressButton, false)
            .With(CardPart.MoveOnHoldButton, false).With(CardPart.MoveWaitingButton, false).With(CardPart.FlagButton, false);

        Assert.Equal("Row", Place(181.0, doneAndDelete));
        Assert.Equal("Side", Place(181.0, CardPartsShown.All));
        Assert.Equal("Side", Place(181.0, doneAndDelete, showButtons: false));   // Hide Buttons
        Assert.Equal("Side", Place(DependencyProperty.UnsetValue, doneAndDelete));
    }
}

[Collection(WpfCollection.Name)]
public sealed class CardButtonRowPanelTests(WpfDispatcherFixture wpf)
{
    [Fact]
    public void ThePanel_LaysOutOnlyTheVisibleButtons_InTheRowsSplitRowsGives() => wpf.Run(() =>
    {
        var panel = new CardButtonRowPanel();
        var buttons = Enumerable.Range(0, 6).Select(_ => new Button { Width = 23, Height = 21, Margin = new Thickness(0, 0, 3, 0) }).ToList();
        foreach (var button in buttons) panel.Children.Add(button);
        CardButtonRowPanel.SetStartsGroup(buttons[4], true);

        Point At(FrameworkElement element) => LayoutInformation.GetLayoutSlot(element).Location;
        void Lay(double width)
        {
            panel.Measure(new Size(width, double.PositiveInfinity));
            panel.Arrange(new Rect(panel.DesiredSize));
        }

        Lay(200);                                                   // all six on one row
        Assert.Equal(21.0, panel.DesiredSize.Height);
        Assert.Equal(new Point(130, 0), At(buttons[5]));

        Lay(110);                                                   // four to a row: the moves, then Done and Delete
        Assert.Equal(45.0, panel.DesiredSize.Height);
        Assert.Equal(new Point(0, 24), At(buttons[4]));

        buttons[3].Visibility = Visibility.Collapsed;               // three moves left: 3 + 2
        Lay(110);
        Assert.Equal(new Point(52, 0), At(buttons[2]));
        Assert.Equal(new Point(0, 24), At(buttons[4]));

        foreach (var button in buttons.Take(4)) button.Visibility = Visibility.Collapsed;   // Done and Delete only
        Lay(110);
        Assert.Equal(21.0, panel.DesiredSize.Height);
        Assert.Equal(new Point(0, 0), At(buttons[4]));
    });
}
