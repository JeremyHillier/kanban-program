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
            Loaded += (_, _) =>
            {
                if (FindName(scrollToSection) is FrameworkElement target)
                {
                    target.BringIntoView();
                }
            };
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }
}
