using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The Timeline's pages saved as a PDF or as pictures.
[Collection(WpfCollection.Name)]
public sealed class PageExportTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static FixedDocument Document(PageSetup setup, int pages)
    {
        var document = new FixedDocument();
        for (var i = 0; i < pages; i++)
        {
            var canvas = new Canvas { Width = setup.PageWidth, Height = setup.PageHeight, Background = Brushes.White };
            var box = new Rectangle { Width = 200, Height = 80, Fill = Brushes.SteelBlue };
            Canvas.SetLeft(box, 40);
            Canvas.SetTop(box, 40);
            canvas.Children.Add(box);

            var page = new FixedPage { Width = setup.PageWidth, Height = setup.PageHeight };
            page.Children.Add(canvas);
            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            document.Pages.Add(content);
        }
        return document;
    }

    [Fact]
    public void ThePages_AreDrawnAtTwiceScreenResolution_OnePerPage() => wpf.Run(() =>
    {
        var setup = new PageSetup(PageSetup.Letter, Landscape: true);
        var pages = PageExport.RenderPages(Document(setup, 2));

        Assert.Equal(2, pages.Count);
        Assert.Equal((2112, 1632), (pages[0].PixelWidth, pages[0].PixelHeight));
    });

    [Fact]
    public void APdf_HasOnePageOfTheChosenPaperSize_PerPicture() => wpf.Run(() =>
    {
        var setup = new PageSetup(PageSetup.Tabloid, Landscape: false);
        var path = _temp.File("timeline.pdf");

        PageExport.SavePdf(path, PageExport.RenderPages(Document(setup, 2)), setup);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(path, PdfSharp.Pdf.IO.PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(2, pdf.PageCount);
        Assert.Equal((792d, 1224d), (Math.Round(pdf.Pages[0].Width.Point), Math.Round(pdf.Pages[0].Height.Point))); // 11 x 17 in
    });

    [Fact]
    public void Pictures_AreOneFileForOnePage_AndNumberedFilesForSeveral() => wpf.Run(() =>
    {
        var setup = new PageSetup(PageSetup.A4, Landscape: true);

        Assert.Equal([_temp.File("one.png")], PageExport.SavePng(_temp.File("one.png"), PageExport.RenderPages(Document(setup, 1))));

        var several = PageExport.SavePng(_temp.File("many.png"), PageExport.RenderPages(Document(setup, 3)));
        Assert.Equal(["many-1.png", "many-2.png", "many-3.png"], several.Select(f => System.IO.Path.GetFileName(f)));
        Assert.All(several, file => Assert.Equal(0x89, File.ReadAllBytes(file)[0])); // the PNG signature
    });
}
