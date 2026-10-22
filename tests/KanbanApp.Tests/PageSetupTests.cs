using System.Globalization;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The paper the Timeline is printed on: sizes, which way round, and what is remembered.
public sealed class PageSetupTests
{
    [Fact]
    public void ThePage_IsThePaperSize_TheRightWayRound()
    {
        var landscape = new PageSetup(PageSetup.Letter, Landscape: true);
        var portrait = new PageSetup(PageSetup.Tabloid, Landscape: false);

        Assert.Equal((1056d, 816d), (landscape.PageWidth, landscape.PageHeight));
        Assert.Equal((1056d, 1632d), (portrait.PageWidth, portrait.PageHeight));
        Assert.Equal(793.7, new PageSetup(PageSetup.A4, false).PageWidth, 1);
    }

    [Fact]
    public void TheChoice_IsSavedAndReadBack()
    {
        var setup = new PageSetup(PageSetup.A3, Landscape: false);

        Assert.Equal("A3,Portrait", setup.ToSetting());
        Assert.Equal(setup, PageSetup.Parse(setup.ToSetting(), new RegionInfo("CA")));
    }

    [Fact]
    public void WithNothingSaved_ItIsThisPlacesUsualPaper_Landscape()
    {
        Assert.Equal(new PageSetup(PageSetup.Letter, true), PageSetup.Parse(null, new RegionInfo("CA")));
        Assert.Equal(new PageSetup(PageSetup.A4, true), PageSetup.Parse("nonsense", new RegionInfo("GB")));
    }
}
