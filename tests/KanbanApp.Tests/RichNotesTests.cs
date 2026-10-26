using System.Windows;
using System.Windows.Documents;
using KanbanApp.Services;
using KanbanApp.Views;

namespace KanbanApp.Tests;

// The Notes with formatting: the model, its plain text, its storage beside the plain Notes, and the editor's document.
[Collection(WpfCollection.Name)]
public sealed class RichNotesTests(WpfDispatcherFixture wpf) : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static RichParagraph Para(string text, NotesAlign align = NotesAlign.Left, bool bold = false, bool italic = false, bool underline = false) =>
        new(align, [new RichRun(text, bold, italic, underline)]);

    private static List<RichBlock> Sample() =>
    [
        RichBlock.Of(new RichParagraph(NotesAlign.Left, [new RichRun("Quotes: "), new RichRun("Zenith", true, true)])),
        RichBlock.Of(new RichTable([[Para("Vendor", bold: true), Para("Price", NotesAlign.Right, bold: true)], [Para("Acme"), Para("1,200", NotesAlign.Right)]])),
        RichBlock.Of(Para("Decide by Friday", NotesAlign.Center, underline: true))
    ];

    [Fact]
    public void ThePlainText_IsALinePerParagraph_AndATableIsTabSeparatedRows()
    {
        Assert.Equal("Quotes: Zenith\r\nVendor\tPrice\r\nAcme\t1,200\r\nDecide by Friday", RichNotes.ToPlainText(Sample()));
        Assert.Equal("a b\r\nc", RichNotes.ToPlainText([RichBlock.Of(Para("a\nb")), RichBlock.Of(Para("c"))]).Replace("a\r\nb", "a b")); // a line break is a line
    }

    [Fact]
    public void PlainTextAsParagraphs_HasNothingToStore()
    {
        var blocks = RichNotes.FromPlainText("one\r\n\r\ntwo");

        Assert.Equal(3, blocks.Count);
        Assert.True(RichNotes.IsPlain(blocks));
        Assert.False(RichNotes.IsPlain(Sample()));
        Assert.False(RichNotes.IsPlain([RichBlock.Of(Para("centred", NotesAlign.Center))]));
        Assert.False(RichNotes.IsPlain([RichBlock.Of(Para("bold", bold: true))]));
    }

    [Fact]
    public void TheStoredForm_ReadsBackTheSame_AndAnythingElseIsNothing()
    {
        var back = RichNotes.Deserialize(RichNotes.Serialize(Sample()))!;

        Assert.Equal(RichNotes.ToPlainText(Sample()), RichNotes.ToPlainText(back));
        Assert.Equal(NotesAlign.Right, back[1].Table!.Rows[0][1].Align);
        Assert.True(back[0].Paragraph!.Runs[1].Bold && back[0].Paragraph!.Runs[1].Italic);
        Assert.True(back[2].Paragraph!.Runs[0].Underline);

        Assert.Null(RichNotes.Deserialize(null));
        Assert.Null(RichNotes.Deserialize("<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"/>")); // nothing that is not ours is ever read
        Assert.Null(RichNotes.Deserialize("[1,2"));
    }

    [Fact]
    public void TheFormatting_IsKeptBesideTheNotes_AndIgnoredOnceTheyChange()
    {
        var db = new DatabaseService(_temp.File("board.db"));
        var column = db.GetColumns().First();
        var card = db.AddCard(column.Id, "Quote comparison", null, column.Name, "Normal", null, null, false, null, null, "Quotes: Zenith");

        Assert.NotEqual(DatabaseService.RichNotesFileFormat.ToString(), db.GetSetting("FileFormat")); // not stamped until formatting is saved

        db.SetRichNotes(card.Id, "Quotes: Zenith", RichNotes.Serialize(Sample()));
        Assert.NotNull(db.GetRichNotes(card.Id, "Quotes: Zenith"));
        Assert.Equal(DatabaseService.RichNotesFileFormat.ToString(), db.GetSetting("FileFormat"));

        Assert.Null(db.GetRichNotes(card.Id, "Notes written by something else")); // no longer the same words
        Assert.Null(db.GetRichNotes(card.Id, null));

        db.SetRichNotes(card.Id, "Quotes: Zenith", null);
        Assert.Null(db.GetRichNotes(card.Id, "Quotes: Zenith"));
    }

    [Fact]
    public void TheEditorsDocument_RoundTripsThroughTheModel_AndPastedExtrasAreReducedToText() => wpf.Run(() =>
    {
        var document = new FlowDocument();
        NotesDocument.Fill(document, Sample());

        Assert.Equal(RichNotes.ToPlainText(Sample()), RichNotes.ToPlainText(NotesDocument.Read(document).Where(b => b.Paragraph?.Text != "" || b.Table is not null).ToList()));
        var read = NotesDocument.Read(document);
        Assert.NotNull(read[1].Table);
        Assert.Equal(NotesAlign.Center, read[2].Paragraph!.Align);

        // A pasted paragraph with a font, a colour and a list keeps only what the notes can hold.
        var pasted = new FlowDocument(new Paragraph(new Run("styled") { FontFamily = new System.Windows.Media.FontFamily("Comic Sans MS"), Foreground = System.Windows.Media.Brushes.Red, FontWeight = FontWeights.Bold }));
        pasted.Blocks.Add(new List(new ListItem(new Paragraph(new Run("item")))));
        var reduced = NotesDocument.Read(pasted);

        Assert.Equal("styled\r\nitem", RichNotes.ToPlainText(reduced));
        Assert.True(reduced[0].Paragraph!.Runs[0].Bold);
    });

    [Fact]
    public void DraggingALine_TradesWidthBetweenTheTwoColumnsBesideIt_AndStopsAtTheMinimum()
    {
        double[] equal = [1, 1, 1];

        // 188 px per unit: 94 px to the right is half a unit.
        Assert.Equal([1.5, 0.5, 1], TableColumns.Resize(equal, 1, 94, 188, 30));
        Assert.Equal([0.5, 1.5, 1], TableColumns.Resize(equal, 1, -94, 188, 30));
        Assert.Equal([1, 1.5, 0.5], TableColumns.Resize(equal, 2, 94, 188, 30));

        // Never narrower than 30 px (0.16 of a unit), whatever the mouse does; the total stays 2.
        var far = TableColumns.Resize(equal, 1, 5000, 188, 30);
        Assert.Equal(2 - 30 / 188d, far[0], 6);
        Assert.Equal(30 / 188d, far[1], 6);
        Assert.Equal(1, far[2]);

        Assert.Equal(equal, TableColumns.Resize(equal, 0, 50, 188, 30)); // there is no line before the first column
        Assert.Equal(equal, TableColumns.Resize(equal, 3, 50, 188, 30));
    }

    [Fact]
    public void ColumnWeights_AreScaledToAnAverageOfOne_AndKeptWithTheTable()
    {
        Assert.Equal([0.5, 1, 1.5], TableColumns.Normalise([100, 200, 300]));
        Assert.Equal([1, 1], TableColumns.Normalise([0, 0]));

        var table = new RichTable([[Para("a"), Para("b")]], [1.4, 0.6]);
        var back = RichNotes.Deserialize(RichNotes.Serialize([RichBlock.Of(table)]))!;
        Assert.Equal([1.4, 0.6], back[0].Table!.ColumnWeights);

        // An older note, or a damaged list of weights, shares the width equally.
        Assert.Null(RichNotes.Deserialize(RichNotes.Serialize([RichBlock.Of(new RichTable([[Para("a"), Para("b")]]))]))![0].Table!.ColumnWeights);
        Assert.Null(RichNotes.Deserialize("[{\"T\":[[{\"R\":[]},{\"R\":[]}]],\"W\":[1,-2]}]")![0].Table!.ColumnWeights);
        Assert.Null(RichNotes.Deserialize("[{\"T\":[[{\"R\":[]},{\"R\":[]}]],\"W\":[1,1,1]}]")![0].Table!.ColumnWeights); // wrong number of columns
    }

    [Fact]
    public void TheEditorsTable_KeepsItsColumnWidths_ThroughTheModel() => wpf.Run(() =>
    {
        var document = new FlowDocument();
        NotesDocument.Fill(document, [RichBlock.Of(new RichTable([[Para("a"), Para("b"), Para("c")]], [2, 0.5, 0.5]))]);

        var read = NotesDocument.Read(document)[0].Table!;
        Assert.Equal(3, read.ColumnWeights!.Count);
        Assert.Equal(3, read.ColumnWeights.Sum(), 6);
        Assert.Equal(2 * 3 / 3d, read.ColumnWeights[0], 6);
        Assert.Equal(0.5, read.ColumnWeights[1], 6);
    });
}
