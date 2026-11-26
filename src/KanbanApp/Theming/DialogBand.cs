using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace KanbanApp.Theming;

// The title band across the top of every dialog: the window's title in capitals, white on the program's blue, with a gold
// line beneath it, inset by the same 12 as the dialog's contents.
//
// Added around each dialog's own content when it loads, as the copyright line is, so no dialog draws a band of its own and
// a new dialog gets one without doing anything. Title says more than the window's title where a dialog needs it ("Import
// Tasks from Excel" on Import Tasks); Note puts a short line at the band's right, such as the version on Help.
public static class DialogBand
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.RegisterAttached("Title", typeof(string), typeof(DialogBand), new PropertyMetadata(null));

    public static void SetTitle(DependencyObject element, string? value) => element.SetValue(TitleProperty, value);
    public static string? GetTitle(DependencyObject element) => (string?)element.GetValue(TitleProperty);

    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.RegisterAttached("Note", typeof(string), typeof(DialogBand), new PropertyMetadata(null));

    public static void SetNote(DependencyObject element, string? value) => element.SetValue(NoteProperty, value);
    public static string? GetNote(DependencyObject element) => (string?)element.GetValue(NoteProperty);

    // Set once a window has its band, so a window shown a second time is not given another.
    private static readonly DependencyProperty AddedProperty =
        DependencyProperty.RegisterAttached("Added", typeof(bool), typeof(DialogBand), new PropertyMetadata(false));

    // The board, the splash and Quick Add are not dialogs: the board has its own header, the other two no title bar.
    private static readonly HashSet<string> ExcludedWindows = ["MainWindow", "SplashWindow", "QuickAddWindow"];

    // The band's measurements, kept together so the tests can hold them.
    internal static readonly Thickness BandMargin = new(12, 12, 12, 0);
    internal static readonly Thickness BandPadding = new(16, 10, 16, 10);
    internal static readonly Thickness RuleThickness = new(0, 0, 0, 2);
    internal const double CornerSize = 4;
    internal const double TitleSize = 17;

    public static void Register()
    {
        // Loaded is a direct event: this runs for the window itself, never for a child's Loaded passing through.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
    }

    internal static bool Wants(Window window) =>
        window.WindowStyle != WindowStyle.None && !ExcludedWindows.Contains(window.GetType().Name);

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window && Wants(window)) Add(window);
    }

    // Moves the window's content under a new band. The content itself is not touched, so any layout works.
    internal static void Add(Window window)
    {
        if ((bool)window.GetValue(AddedProperty) || window.Content is not UIElement content) return;
        window.SetValue(AddedProperty, true);

        var band = Build(window);
        DockPanel.SetDock(band, Dock.Top);
        var host = new DockPanel { LastChildFill = true };
        window.Content = null; // an element can only have one parent
        host.Children.Add(band);
        host.Children.Add(content);
        window.Content = host;

        // A window sized to its content grows by the band now, so anything that places it on the screen next sees its real height.
        if (window.SizeToContent != SizeToContent.Manual) window.UpdateLayout();
    }

    internal static Border Build(Window window)
    {
        var title = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = TitleSize,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        // DialogBand.Title when the window sets one, else its own Title; either is followed if it changes.
        var text = new MultiBinding { Converter = CapitalsOfFirst.Instance };
        text.Bindings.Add(new Binding { Source = window, Path = new PropertyPath(TitleProperty) });
        text.Bindings.Add(new Binding { Source = window, Path = new PropertyPath(Window.TitleProperty) });
        title.SetBinding(TextBlock.TextProperty, text);

        // The note gives way before the title does: it is the one trimmed first.
        var note = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF5)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        note.SetBinding(TextBlock.TextProperty, new Binding { Source = window, Path = new PropertyPath(NoteProperty) });
        DockPanel.SetDock(note, Dock.Right);

        var row = new DockPanel { LastChildFill = true };
        row.Children.Add(note);
        row.Children.Add(title);

        var band = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(CornerSize),
            Padding = BandPadding,
            Margin = BandMargin,
            BorderThickness = RuleThickness,
            SnapsToDevicePixels = true,
        };
        // Resource references, so the band follows a switch between the light and dark themes.
        band.SetResourceReference(Border.BackgroundProperty, "DialogBandBrush");
        band.SetResourceReference(Border.BorderBrushProperty, "DialogBandRuleBrush");
        return band;
    }

    // The first of the values that has any text, in capitals.
    private sealed class CapitalsOfFirst : IMultiValueConverter
    {
        public static readonly CapitalsOfFirst Instance = new();

        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
            (values.OfType<string>().FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty).ToUpper(culture);

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
