using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using KanbanApp.Services;

namespace KanbanApp.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        CopyrightText.Text = AppInfo.Copyright;

        var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/Assets/app.ico"),
            BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        LogoImage.Source = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
    }
}
