using System.Windows;
using System.Windows.Media;
using KanbanApp.Theming;

namespace KanbanApp.Tests;

// The foot of the button column and the dialogs' copyright line have their own colours in both themes.
[Collection(WpfCollection.Name)]
public sealed class FooterColourTests(WpfDispatcherFixture wpf)
{
    private static Color ColourOf(string key) => ((SolidColorBrush)Application.Current.Resources[key]).Color;

    [Fact]
    public void TheLightTheme_UsesTheDarkerFooterGrey_TheWindowsLinkBlue_AndASlateCopyrightLine() => wpf.Run(() =>
    {
        ThemeManager.Apply(isDark: true);
        Assert.Equal(Color.FromRgb(0xAA, 0xAA, 0xAA), ColourOf("FooterTextBrush"));
        Assert.Equal(Color.FromRgb(0x64, 0xB5, 0xF6), ColourOf("FooterLinkBrush"));
        Assert.Equal(Color.FromRgb(0xAA, 0xAA, 0xAA), ColourOf("DialogCopyrightBrush"));

        ThemeManager.Apply(isDark: false);
        Assert.Equal(Color.FromRgb(0x66, 0x66, 0x66), ColourOf("FooterTextBrush"));
        Assert.Equal(SystemColors.HotTrackColor, ColourOf("FooterLinkBrush"));
        Assert.Equal(Color.FromRgb(0x5A, 0x6B, 0x7B), ColourOf("DialogCopyrightBrush"));
    });
}
