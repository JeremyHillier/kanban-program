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
        BuildTopics();

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

    // ---- The topic list down the left ----

    // The section headings in the text, in order, each with the element to jump to. "Overview" is the
    // few lines before the first heading, so it points at the very top.
    private readonly List<(string Title, FrameworkElement? Target)> _topics = [];
    private bool _syncingTopics;
    private bool _jumped; // the next scroll came from a click on a topic, so the list is already right

    internal IReadOnlyList<string> TopicTitles => [.. _topics.Select(t => t.Title)];

    private void BuildTopics()
    {
        var headerStyle = (Style)FindResource("HelpSectionHeaderStyle");
        _topics.Add(("Overview", null));
        foreach (var heading in HeadingsIn(ContentPanel).Where(h => ReferenceEquals(h.Style, headerStyle) && !string.IsNullOrWhiteSpace(h.Text)))
            _topics.Add((heading.Text, heading));

        _syncingTopics = true;
        TopicList.ItemsSource = _topics.Select(t => t.Title).ToList();
        TopicList.SelectedIndex = 0;
        _syncingTopics = false;
    }

    private static IEnumerable<TextBlock> HeadingsIn(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is TextBlock block) yield return block;
            foreach (var inner in HeadingsIn(child)) yield return inner;
        }
    }

    private double OffsetOf(FrameworkElement? target) => target is null ? 0 : target.TranslatePoint(new Point(0, 0), ContentPanel).Y;

    // Clicking (or arrowing to) a topic puts its heading at the top of the text.
    private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingTopics || TopicList.SelectedIndex < 0) return;
        var offset = OffsetOf(_topics[TopicList.SelectedIndex].Target);
        // A click near the end can't scroll as far as its heading, and the follow-the-scroll rule would then
        // select the last topic instead; so the scroll this causes is left alone. (No scroll, no event to swallow.)
        _jumped = Math.Abs(Math.Min(offset, ContentScrollViewer.ScrollableHeight) - ContentScrollViewer.VerticalOffset) > 0.5;
        ContentScrollViewer.ScrollToVerticalOffset(offset);
    }

    // As the text scrolls, the list follows: the last heading that has reached the top is the one in view.
    private void ContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_topics.Count == 0 || e.VerticalChange == 0 && e.ExtentHeightChange == 0) return;
        if (_jumped) { _jumped = false; return; }

        var top = ContentScrollViewer.VerticalOffset + 24;
        var current = 0;
        for (var i = 0; i < _topics.Count; i++)
        {
            if (OffsetOf(_topics[i].Target) <= top) current = i;
        }
        if (ContentScrollViewer.VerticalOffset >= ContentScrollViewer.ScrollableHeight - 1 && ContentScrollViewer.ScrollableHeight > 0) current = _topics.Count - 1;

        if (current == TopicList.SelectedIndex) return;
        _syncingTopics = true;
        TopicList.SelectedIndex = current;
        TopicList.ScrollIntoView(TopicList.SelectedItem);
        _syncingTopics = false;
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
