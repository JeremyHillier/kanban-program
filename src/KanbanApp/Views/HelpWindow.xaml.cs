using System.Windows;
using System.Windows.Controls;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Help, in two tabs: How it works, and What's New (the last few versions of the changelog embedded
// in the exe, see ReleaseNotes). After an update the window opens on What's New.
public partial class HelpWindow : Window
{
    private readonly MainViewModel _viewModel;

    // scrollToSection, when given, is the x:Name of a section header on the How it works tab (e.g.
    // "HelpSection_ReportBuilder") - lets a window's own Help button open straight to its topic
    // instead of the top of the whole Help screen.
    public HelpWindow(MainViewModel viewModel, string? scrollToSection = null, bool showWhatsNew = false)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        _viewModel = viewModel;
        DataContext = viewModel;
        VersionText.Text = $"Version {viewModel.AppVersion.TrimStart('v')}";
        RenderWhatsNew();

        if (showWhatsNew)
        {
            Tabs.SelectedItem = WhatsNewTab;
            Title = "What's New";
            HeadingText.Text = "WHAT'S NEW";
        }

        if (scrollToSection is not null)
        {
            // BringIntoView only scrolls the minimum distance needed to make the target visible,
            // which for a section past the first screenful lands it at the bottom of the viewport
            // instead of the top - scrolling to its exact offset within the content puts the
            // section header flush at the top instead, however far down the topic sits.
            Loaded += (_, _) =>
            {
                if (FindName(scrollToSection) is FrameworkElement target)
                {
                    var offset = target.TranslatePoint(new Point(0, 0), ContentPanel).Y;
                    ContentScrollViewer.ScrollToVerticalOffset(offset);
                }
            };
        }
    }

    // The last few versions only: what changed recently is news, the rest is history.
    private void RenderWhatsNew()
    {
        var notes = ReleaseNotes.Load(MainViewModel.WhatsNewVersionCount);
        if (notes.Count == 0)
        {
            WhatsNewPanel.Children.Add(new TextBlock { Text = "No release notes are available in this build.", TextWrapping = TextWrapping.Wrap });
            return;
        }

        foreach (var note in notes)
        {
            WhatsNewPanel.Children.Add(new TextBlock
            {
                Text = $"{note.Version} — {note.Date}",
                Style = (Style)FindResource("HelpSectionHeaderStyle"),
            });
            foreach (var item in note.Items) WhatsNewPanel.Children.Add(new BulletItem { Text = item });
        }

        var all = ReleaseNotes.Load(int.MaxValue).Count;
        if (all > notes.Count)
        {
            var footnote = new TextBlock
            {
                Text = $"The last {notes.Count} versions are shown, of {all}.",
                FontSize = 11,
                Margin = new Thickness(0, 14, 0, 0),
            };
            footnote.SetResourceReference(ForegroundProperty, "SecondaryTextStrongBrush");
            WhatsNewPanel.Children.Add(footnote);
        }
    }

    private void ReportProblem_Click(object sender, RoutedEventArgs e)
    {
        ProblemReport.Compose(this, _viewModel.CurrentDbPath);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }
}
