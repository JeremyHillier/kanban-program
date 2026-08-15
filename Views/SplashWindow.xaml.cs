using System.Reflection;
using System.Windows;

namespace KanbanApp.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        CopyrightText.Text = "© Jeremy Hillier Consulting Inc";
    }
}
