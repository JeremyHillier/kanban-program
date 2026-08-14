using System.Windows;
using System.Windows.Media;

namespace KanbanApp.Theming;

public static class ThemeManager
{
    public static void Apply(bool isDark)
    {
        var res = Application.Current.Resources;

        if (isDark)
        {
            res["WindowBackgroundBrush"] = Brush(0x1E, 0x1E, 0x1E);
            res["CardBackgroundBrush"] = Brush(0x2D, 0x2D, 0x30);
            res["CardBorderBrush"] = Brush(0x3F, 0x3F, 0x46);
            res["PrimaryTextBrush"] = Brush(0xF0, 0xF0, 0xF0);
            res["SecondaryTextBrush"] = Brush(0xAA, 0xAA, 0xAA);
            res["PanelBackgroundBrush"] = Brush(0x25, 0x25, 0x26);
            res["PanelBorderBrush"] = Brush(0x6E, 0x6E, 0x6E);
            res["ButtonBackgroundBrush"] = Brush(0x3F, 0x3F, 0x46);
            res["InputBackgroundBrush"] = Brush(0x33, 0x33, 0x36);
        }
        else
        {
            res["WindowBackgroundBrush"] = Brush(0xF1, 0xF2, 0xF5);
            res["CardBackgroundBrush"] = Brushes.White;
            res["CardBorderBrush"] = Brush(0xD0, 0xD0, 0xD0);
            res["PrimaryTextBrush"] = Brush(0x1A, 0x1A, 0x1A);
            res["SecondaryTextBrush"] = Brush(0x88, 0x88, 0x88);
            res["PanelBackgroundBrush"] = Brush(0xFA, 0xFA, 0xFA);
            res["PanelBorderBrush"] = Brush(0x33, 0x33, 0x33);
            res["ButtonBackgroundBrush"] = Brush(0xEC, 0xEC, 0xEC);
            res["InputBackgroundBrush"] = Brushes.White;
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
