using System.Globalization;
using System.Printing;

namespace KanbanApp.Services;

// A paper size the print preview offers, in inches, with the name Windows' printing uses for it.
public sealed record PaperSize(string Key, string Name, double WidthInches, double HeightInches, PageMediaSizeName MediaName)
{
    public override string ToString() => Name;
}

// The paper and the way round to print on. A document is laid out for it (its page size in WPF units of
// 1/96 inch), and the printer is asked for the same paper, so nothing is scaled or cut off.
public sealed record PageSetup(PaperSize Paper, bool Landscape)
{
    public static readonly PaperSize Letter = new("Letter", "Letter (8.5 x 11 in)", 8.5, 11, PageMediaSizeName.NorthAmericaLetter);
    public static readonly PaperSize Legal = new("Legal", "Legal (8.5 x 14 in)", 8.5, 14, PageMediaSizeName.NorthAmericaLegal);
    public static readonly PaperSize Tabloid = new("Tabloid", "Tabloid (11 x 17 in)", 11, 17, PageMediaSizeName.NorthAmericaTabloid);
    public static readonly PaperSize A4 = new("A4", "A4 (210 x 297 mm)", 210 / 25.4, 297 / 25.4, PageMediaSizeName.ISOA4);
    public static readonly PaperSize A3 = new("A3", "A3 (297 x 420 mm)", 297 / 25.4, 420 / 25.4, PageMediaSizeName.ISOA3);

    public static readonly PaperSize[] Papers = [Letter, Legal, Tabloid, A4, A3];

    public double PageWidth => (Landscape ? Paper.HeightInches : Paper.WidthInches) * 96;

    public double PageHeight => (Landscape ? Paper.WidthInches : Paper.HeightInches) * 96;

    // Letter in North America, where it is the everyday paper, A4 everywhere else.
    public static PaperSize LocalPaper(RegionInfo region) => region.TwoLetterISORegionName is "US" or "CA" or "MX" or "PH" ? Letter : A4;

    // Saved as "Letter,Landscape"; anything unreadable gives this PC's usual paper, landscape.
    public static PageSetup Parse(string? saved, RegionInfo region)
    {
        var parts = (saved ?? string.Empty).Split(',');
        var paper = Papers.FirstOrDefault(p => p.Key == parts[0]) ?? LocalPaper(region);
        var landscape = parts.Length < 2 || parts[1] != "Portrait";
        return new PageSetup(paper, landscape);
    }

    public string ToSetting() => $"{Paper.Key},{(Landscape ? "Landscape" : "Portrait")}";
}
