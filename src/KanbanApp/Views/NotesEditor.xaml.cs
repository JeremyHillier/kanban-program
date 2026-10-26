using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using KanbanApp.Services;

namespace KanbanApp.Views;

// The Notes box of the task window: a RichTextBox with a small bar (bold, italic, underline, alignment, tables) over it.
// The window reads it as the notes model (Blocks) and as plain text (PlainText); see RichNotes.
public partial class NotesEditor : UserControl
{
    private bool _updatingBar;

    public NotesEditor()
    {
        InitializeComponent();
        foreach (var n in Enumerable.Range(1, 20)) RowsBox.Items.Add(n);
        foreach (var n in Enumerable.Range(1, 8)) ColumnsBox.Items.Add(n);
        RowsBox.SelectedIndex = 2;
        ColumnsBox.SelectedIndex = 1;
        Box.Document = NewDocument();
        Box.TextChanged += (_, _) => StyleTables();
    }

    public RichTextBox TextBox => Box;

    // The text as typed, in the notes model; setting it replaces the box's contents (and its undo history).
    public List<RichBlock> Blocks
    {
        get => NotesDocument.Read(Box.Document);
        set
        {
            var document = NewDocument();
            NotesDocument.Fill(document, value);
            Box.Document = document;
        }
    }

    public string PlainText => RichNotes.ToPlainText(Blocks);

    // A table pasted from Excel or Word arrives with its own look - often no cell lines at all. Every table in the box is
    // given the notes' own lines and padding once (marked through its Tag), so it reads as a grid before it is ever saved.
    private bool _styling;

    private void StyleTables()
    {
        if (_styling) return;
        var tables = Box.Document.Blocks.OfType<Table>().Where(t => !Equals(t.Tag, NotesDocument.StyledTag)).ToList();
        if (tables.Count == 0) return;

        _styling = true;
        try
        {
            foreach (var table in tables) NotesDocument.Style(table);
        }
        finally
        {
            _styling = false;
        }
    }

    private static FlowDocument NewDocument() => new() { PagePadding = new Thickness(0), LineHeight = double.NaN };

    public void FocusAtEnd()
    {
        Box.Focus();
        Box.CaretPosition = Box.Document.ContentEnd;
    }

    // --- The bar ----------------------------------------------------------------------------------------

    private void Bold_Click(object sender, RoutedEventArgs e) => Apply(EditingCommands.ToggleBold);

    private void Italic_Click(object sender, RoutedEventArgs e) => Apply(EditingCommands.ToggleItalic);

    private void Underline_Click(object sender, RoutedEventArgs e) => Apply(EditingCommands.ToggleUnderline);

    private void Left_Click(object sender, RoutedEventArgs e) => Apply(EditingCommands.AlignLeft);

    private void Center_Click(object sender, RoutedEventArgs e) => Apply(EditingCommands.AlignCenter);

    private void Right_Click(object sender, RoutedEventArgs e) => Apply(EditingCommands.AlignRight);

    private void Apply(RoutedCommand command)
    {
        command.Execute(null, Box);
        Box.Focus();
        ShowState();
    }

    private void Box_SelectionChanged(object sender, RoutedEventArgs e) => ShowState();

    // The bar's buttons follow the text under the cursor (or the selection, when it all looks the same).
    private void ShowState()
    {
        if (_updatingBar) return;
        _updatingBar = true;
        try
        {
            var selection = Box.Selection;
            BoldButton.IsChecked = selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight && weight.ToOpenTypeWeight() >= 600;
            ItalicButton.IsChecked = selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle style && style == FontStyles.Italic;
            UnderlineButton.IsChecked = selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations
                && decorations.Any(d => d.Location == TextDecorationLocation.Underline);

            var align = selection.GetPropertyValue(Block.TextAlignmentProperty) as TextAlignment?;
            LeftButton.IsChecked = align is TextAlignment.Left;
            CenterButton.IsChecked = align is TextAlignment.Center;
            RightButton.IsChecked = align is TextAlignment.Right;
        }
        finally
        {
            _updatingBar = false;
        }
    }

    // --- Tables -----------------------------------------------------------------------------------------

    private TableCell? CurrentCell()
    {
        TextElement? element = Box.CaretPosition.Paragraph;
        while (element is not null && element is not TableCell) element = element.Parent as TextElement;
        return element as TableCell;
    }

    private static (TableRow Row, Table Table)? Locate(TableCell cell) =>
        cell.Parent is TableRow { Parent: TableRowGroup { Parent: Table table } } row ? (row, table) : null;

    private void TablePopup_Opened(object sender, EventArgs e)
    {
        var inTable = CurrentCell() is not null;
        foreach (var button in new[] { AddRowButton, AddColumnButton, DeleteRowButton, DeleteColumnButton, DeleteTableButton }) button.IsEnabled = inTable;
        TableHint.Visibility = inTable ? Visibility.Collapsed : Visibility.Visible;
    }

    private void InsertTable_Click(object sender, RoutedEventArgs e)
    {
        var table = NotesDocument.Build(RichNotes.EmptyTable((int)RowsBox.SelectedItem, (int)ColumnsBox.SelectedItem));

        // After the block the cursor is in (after the whole table, if it is in one).
        TextElement? anchor = Box.CaretPosition.Paragraph;
        while (anchor?.Parent is TextElement parent) anchor = parent;

        Box.BeginChange();
        try
        {
            if (anchor is Block block) Box.Document.Blocks.InsertAfter(block, table);
            else Box.Document.Blocks.Add(table);
            if (Box.Document.Blocks.LastBlock == table) Box.Document.Blocks.Add(new Paragraph { Margin = new Thickness(0) }); // room to type after it
        }
        finally
        {
            Box.EndChange();
        }

        TablePopup.IsOpen = false;
        MoveInto(table.RowGroups[0].Rows[0].Cells[0]);
    }

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentCell() is { } cell && Locate(cell) is { } here) MoveInto(AddRowBelow(here.Row, here.Table));
        TablePopup.IsOpen = false;
    }

    private TableCell AddRowBelow(TableRow row, Table table)
    {
        var added = new TableRow();
        for (var c = 0; c < table.Columns.Count; c++) added.Cells.Add(NotesDocument.NewCell(new RichParagraph(NotesAlign.Left, [])));

        Box.BeginChange();
        try
        {
            ((TableRowGroup)row.Parent).Rows.Insert(((TableRowGroup)row.Parent).Rows.IndexOf(row) + 1, added);
        }
        finally
        {
            Box.EndChange();
        }
        return added.Cells[0];
    }

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentCell() is { } cell && Locate(cell) is { } here)
        {
            var at = here.Row.Cells.IndexOf(cell) + 1;
            Box.BeginChange();
            try
            {
                here.Table.Columns.Insert(at, new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                foreach (var row in here.Table.RowGroups.SelectMany(g => g.Rows))
                    row.Cells.Insert(Math.Min(at, row.Cells.Count), NotesDocument.NewCell(new RichParagraph(NotesAlign.Left, [])));
            }
            finally
            {
                Box.EndChange();
            }
            MoveInto(here.Row.Cells[Math.Min(at, here.Row.Cells.Count - 1)]);
        }
        TablePopup.IsOpen = false;
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentCell() is { } cell && Locate(cell) is { } here)
        {
            var group = (TableRowGroup)here.Row.Parent;
            var at = group.Rows.IndexOf(here.Row);
            Box.BeginChange();
            try
            {
                group.Rows.Remove(here.Row);
                if (here.Table.RowGroups.All(g => g.Rows.Count == 0)) Box.Document.Blocks.Remove(here.Table);
            }
            finally
            {
                Box.EndChange();
            }
            if (group.Rows.Count > 0) MoveInto(group.Rows[Math.Min(at, group.Rows.Count - 1)].Cells[0]);
        }
        TablePopup.IsOpen = false;
    }

    private void DeleteColumn_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentCell() is { } cell && Locate(cell) is { } here)
        {
            var at = here.Row.Cells.IndexOf(cell);
            Box.BeginChange();
            try
            {
                foreach (var row in here.Table.RowGroups.SelectMany(g => g.Rows).ToList())
                    if (at < row.Cells.Count) row.Cells.RemoveAt(at);
                if (at < here.Table.Columns.Count) here.Table.Columns.RemoveAt(at);
                if (here.Table.Columns.Count == 0) Box.Document.Blocks.Remove(here.Table);
            }
            finally
            {
                Box.EndChange();
            }
            if (here.Table.Columns.Count > 0) MoveInto(here.Row.Cells[Math.Min(at, here.Row.Cells.Count - 1)]);
        }
        TablePopup.IsOpen = false;
    }

    private void DeleteTable_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentCell() is { } cell && Locate(cell) is { } here)
        {
            Box.BeginChange();
            try
            {
                Box.Document.Blocks.Remove(here.Table);
                if (Box.Document.Blocks.Count == 0) Box.Document.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
            }
            finally
            {
                Box.EndChange();
            }
            Box.Focus();
        }
        TablePopup.IsOpen = false;
    }

    private void MoveInto(TableCell cell)
    {
        Box.Focus();
        Box.CaretPosition = cell.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
    }

    // Tab and Shift+Tab move between a table's cells (Tab in the last cell adds a row); elsewhere Tab types a tab.
    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || (Keyboard.Modifiers & ~ModifierKeys.Shift) != ModifierKeys.None) return;
        if (CurrentCell() is not { } cell || Locate(cell) is not { } here) return;

        var cells = here.Table.RowGroups.SelectMany(g => g.Rows).SelectMany(r => r.Cells).ToList();
        var at = cells.IndexOf(cell);
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            if (at > 0) MoveInto(cells[at - 1]);
        }
        else if (at + 1 < cells.Count) MoveInto(cells[at + 1]);
        else MoveInto(AddRowBelow(here.Row, here.Table));

        e.Handled = true;
    }
}
