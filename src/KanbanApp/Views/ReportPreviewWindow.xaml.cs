using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using KanbanApp.Services;

namespace KanbanApp.Views;

public partial class ReportPreviewWindow : Window
{
    private readonly Func<PageSetup, FixedDocument>? _layOut;
    private readonly Action<PageSetup>? _remember;
    private PageSetup? _setup;
    private bool _ready;

    public ReportPreviewWindow(FixedDocument document)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        Viewer.Document = document;
    }

    // A document that can be laid out for any paper: the window offers the paper and orientation, lays the
    // pages out again when either changes (remembering the choice), and prints on that paper.
    public ReportPreviewWindow(Func<PageSetup, FixedDocument> layOut, PageSetup setup, Action<PageSetup> remember)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        _layOut = layOut;
        _remember = remember;
        _setup = setup;

        PageSetupPanel.Visibility = Visibility.Visible;
        PrintButton.Visibility = Visibility.Visible;
        PaperComboBox.ItemsSource = PageSetup.Papers;
        PaperComboBox.SelectedItem = setup.Paper;
        (setup.Landscape ? LandscapeRadio : PortraitRadio).IsChecked = true;

        // The viewer's own print button (and Ctrl+P) would print with the printer's default paper, so both go
        // through Print_Click, which asks for the chosen one.
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (_, _) => Print_Click(this, new RoutedEventArgs())));
        Viewer.CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (_, _) => Print_Click(this, new RoutedEventArgs())));

        _ready = true;
        Viewer.Document = layOut(setup);
    }

    private void PageSetup_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready || _layOut is null || PaperComboBox.SelectedItem is not PaperSize paper) return;

        _setup = new PageSetup(paper, LandscapeRadio.IsChecked == true);
        _remember?.Invoke(_setup);
        Viewer.Document = _layOut(_setup);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (Viewer.Document is not FixedDocument document || _setup is not { } setup) return;

        var dialog = new PrintDialog();
        try
        {
            dialog.PrintTicket.PageMediaSize = new PageMediaSize(setup.Paper.MediaName, setup.Paper.WidthInches * 96, setup.Paper.HeightInches * 96);
            dialog.PrintTicket.PageOrientation = setup.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        }
        catch
        {
            // A printer that will not take the ticket: the dialog still opens, and the user can pick the paper there.
        }

        if (dialog.ShowDialog() != true) return;
        dialog.PrintDocument(document.DocumentPaginator, Title);
    }
}
