using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The attachment right-click menu's file work. Builds what would go on the clipboard without
// putting it there, so running the tests never touches what the user has copied.
[Collection(WpfCollection.Name)]
public sealed class AttachmentFilesTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string WritePng(string name)
    {
        var path = _temp.File(name);
        var pixels = new byte[4 * 4 * 4];
        Array.Fill(pixels, (byte)200);
        var bitmap = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    [Fact]
    public void CopyingAFile_PutsTheFileItselfOnTheClipboard_ReadyToPasteIntoAFolderOrEmail() => wpf.Run(() =>
    {
        var path = _temp.File("notes.txt");
        File.WriteAllText(path, "hello");

        var data = AttachmentFiles.BuildClipboardData(path);

        Assert.Equal([path], data.GetFileDropList().Cast<string>());
        Assert.False(data.ContainsImage());
    });

    [Fact]
    public void CopyingAPicture_AlsoPutsThePictureOn_ForPastingIntoWordOrPaint() => wpf.Run(() =>
    {
        var path = WritePng("screenshot.png");

        var data = AttachmentFiles.BuildClipboardData(path);

        Assert.Equal([path], data.GetFileDropList().Cast<string>());
        Assert.True(data.ContainsImage());
        Assert.Equal(4, data.GetImage().PixelWidth);

        File.Delete(path); // the picture was read in full, so the file isn't left locked
    });

    [Fact]
    public void AFileNamedLikeAPictureButNotOne_StillCopiesAsAFile() => wpf.Run(() =>
    {
        var path = _temp.File("broken.png");
        File.WriteAllText(path, "not really a picture");

        var data = AttachmentFiles.BuildClipboardData(path);

        Assert.Equal([path], data.GetFileDropList().Cast<string>());
        Assert.False(data.ContainsImage());
    });

    [Theory]
    [InlineData("Quote.pdf", "PDF File (*.pdf)|*.pdf|All Files (*.*)|*.*")]
    [InlineData("photo.JPG", "JPG File (*.JPG)|*.JPG|All Files (*.*)|*.*")]
    [InlineData("README", "All Files (*.*)|*.*")]
    public void TheSaveDialog_OffersTheFilesOwnTypeFirst(string name, string expected)
    {
        Assert.Equal(expected, AttachmentFiles.SaveFilter(name));
    }

    [Fact]
    public void SavingACopy_CopiesTheFile_ReplacingOneAlreadyThere_AndLeavesTheAttachmentAlone()
    {
        var source = _temp.File("invoice.pdf");
        File.WriteAllText(source, "the invoice");
        var destination = _temp.File("Desktop copy.pdf");
        File.WriteAllText(destination, "an older file");

        Assert.True(AttachmentFiles.SaveCopy(source, destination));

        Assert.Equal("the invoice", File.ReadAllText(destination));
        Assert.Equal("the invoice", File.ReadAllText(source));
    }

    [Fact]
    public void SavingACopyOverTheAttachmentItself_DoesNothing()
    {
        var source = _temp.File("invoice.pdf");
        File.WriteAllText(source, "the invoice");

        Assert.False(AttachmentFiles.SaveCopy(source, source.ToUpperInvariant()));
        Assert.Equal("the invoice", File.ReadAllText(source));
    }
}
