using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// Tooltips open to the right of the pointer, and only go to its left, or above its level, where the
// screen runs out. Where WPF puts them on a real screen is checked by hand; this pins the order.
[Collection(WpfCollection.Name)]
public sealed class ToolTipPlacementTests(WpfDispatcherFixture wpf)
{
    [Fact]
    public void RightOfThePointer_ComesFirst_ThenLeft_ThenTheSameAbove()
    {
        var places = ToolTipPlacement.Placements(new Point(1000, 500), new Size(300, 40), scale: 1);

        Assert.Equal(
            [new Point(1020, 500), new Point(696, 500), new Point(1020, 460), new Point(696, 460)],
            places.Select(p => p.Point));
        Assert.All(places, p => Assert.Equal(PopupPrimaryAxis.Horizontal, p.PrimaryAxis));
    }

    [Fact]
    public void TheGaps_GrowWithTheScreenScaling()
    {
        var places = ToolTipPlacement.Placements(new Point(1500, 750), new Size(450, 60), scale: 1.5);

        Assert.Equal(1530, places[0].Point.X);      // 20 at 100%, 30 at 150%
        Assert.Equal(1500 - 6 - 450, places[1].Point.X);
    }

    [Fact]
    public void TheToolTipStyle_SwitchesItOn() => wpf.Run(() =>
    {
        var tip = new ToolTip();
        ToolTipPlacement.SetBesidePointer(tip, true);
        Assert.Equal(PlacementMode.Custom, tip.Placement);
        Assert.NotNull(tip.CustomPopupPlacementCallback);

        ToolTipPlacement.SetBesidePointer(tip, false);
        Assert.Equal(PlacementMode.Mouse, tip.Placement);
        Assert.Null(tip.CustomPopupPlacementCallback);
    });
}
