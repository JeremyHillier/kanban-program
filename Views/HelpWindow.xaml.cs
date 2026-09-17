using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class HelpWindow : Window
{
    private readonly MainViewModel _viewModel;

    // scrollToSection, when given, is the x:Name of a section header in this window's XAML (e.g.
    // "HelpSection_ReportBuilder") - lets a dialog's own Help button open straight to its topic
    // instead of the top of the whole Help screen.
    public HelpWindow(MainViewModel viewModel, string? scrollToSection = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

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

    private void ReportProblem_Click(object sender, RoutedEventArgs e)
    {
        Services.ProblemReport.Compose(this, _viewModel.CurrentDbPath);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void WhatsNew_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WhatsNewWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }
}
