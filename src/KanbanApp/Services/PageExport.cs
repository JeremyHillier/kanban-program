using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KanbanApp.Services;

// A laid-out document (the Timeline's pages) saved as a PDF or as picture files, for sharing rather than printing.
// Each page is drawn once to a picture at twice the screen resolution; the PDF holds those pictures on pages of the
// chosen paper size, so it looks as printed. (Its text cannot be selected - the pages are pictures.)
public static class PageExport
{
    private const double Scale = 2; // pixels per WPF unit: 192 dpi, sharp when zoomed or printed

    // One picture per page, in order. The pages are the same FixedPage content the preview shows.
    public static List<BitmapSource> RenderPages(FixedDocument document)
    {
        var pages = new List<BitmapSource>();
        foreach (var content in document.Pages)
        {
            var page = content.Child;
            page.Measure(new Size(page.Width, page.Height));
            page.Arrange(new Rect(0, 0, page.Width, page.Height));
            page.UpdateLayout();

            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(page.Width * Scale), (int)Math.Ceiling(page.Height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
            bitmap.Render(page);
            bitmap.Freeze();
            pages.Add(bitmap);
        }
        return pages;
    }

    private static byte[] Png(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // A single file for a one-page document, else name-1.png, name-2.png ... beside it. Returns the files written.
    public static List<string> SavePng(string path, IReadOnlyList<BitmapSource> pages)
    {
        var written = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var file = pages.Count == 1
                ? path
                : Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(path)}-{i + 1}{Path.GetExtension(path)}");
            File.WriteAllBytes(file, Png(pages[i]));
            written.Add(file);
        }
        return written;
    }

    // One PDF page per picture, sized to the paper (WPF units are 1/96 inch, PDF points 1/72).
    public static void SavePdf(string path, IReadOnlyList<BitmapSource> pages, PageSetup setup)
    {
        var pdf = new PdfDocument();
        foreach (var bitmap in pages)
        {
            var page = pdf.AddPage();
            page.Width = XUnit.FromPoint(setup.PageWidth * 72 / 96);
            page.Height = XUnit.FromPoint(setup.PageHeight * 72 / 96);

            using var stream = new MemoryStream(Png(bitmap));
            using var image = XImage.FromStream(stream);
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        }
        pdf.Save(path);
    }
}
