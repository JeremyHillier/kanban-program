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

    [Theory]
    [InlineData(@"C:\Tools\setup.exe")]
    [InlineData(@"\\server\share\run.bat")]
    [InlineData(@"C:\Scripts\Clean Up.PS1")]
    [InlineData(@"D:\Work\invoice.pdf.exe")]    // a document's name with a program's ending
    [InlineData(@"D:\Work\run.bat.")]            // Windows drops the dot and runs it
    [InlineData(@"D:\Work\run.cmd  ")]
    [InlineData(@"C:\Users\Me\Desktop\App.lnk")]
    [InlineData(@"C:\Links\site.url")]
    [InlineData(@"C:\Setup\tool.msi")]
    [InlineData(@"C:\Setup\fix.reg")]
    [InlineData(@"C:\Setup\page.hta")]
    [InlineData(@"C:\Setup\macro.vbs")]
    [InlineData(@"D:\Work\notes.txt:hidden.exe")] // a hidden part of a file
    public void AProgramOrScript_IsAskedAbout(string path) =>
        Assert.True(AttachmentFiles.CanRunAProgram(path));

    [Theory]
    [InlineData(@"C:\Work\invoice.pdf")]
    [InlineData(@"C:\Work\Budget.XLSX")]
    [InlineData(@"C:\Work\letter.docx")]
    [InlineData(@"C:\Work\photo.jpg")]
    [InlineData(@"\\server\share\plan.png")]
    [InlineData(@"C:\Work\readme.txt")]
    [InlineData(@"C:\Work\archive.zip")]
    [InlineData(@"C:\Work\exe files.pdf")]       // "exe" in the name isn't the ending
    [InlineData(@"C:\Work\no extension")]
    public void ADocumentOrPicture_OpensWithoutAQuestion(string path) =>
        Assert.False(AttachmentFiles.CanRunAProgram(path));

    [Fact]
    public void TheQuestion_DefaultsToNotOpening_AndShowsThePath()
    {
        var question = AttachmentFiles.ProgramQuestion(@"\\server\share\run.bat");

        Assert.True(question.IsDanger);             // Enter chooses the safe button
        Assert.False(question.EnterChoosesMain);
        Assert.Equal("Open Anyway", question.Yes);
        Assert.Equal("Don't Open", question.No);
        Assert.Equal(@"\\server\share\run.bat", question.Detail);
    }
}
