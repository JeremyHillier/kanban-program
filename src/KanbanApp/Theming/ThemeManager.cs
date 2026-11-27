using System.Windows;
using System.Windows.Media;
using KanbanApp.Models;

namespace KanbanApp.Theming;

public static class ThemeManager
{
    public static void Apply(bool isDark)
    {
        var res = Application.Current.Resources;

        // The card background colours, in this theme's shades (CardColors).
        foreach (var colour in CardColors.All)
        {
            var value = isDark ? colour.Dark : colour.Light;
            res[CardColors.ResourceKey(colour.Key)] = Brush((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }

        if (isDark)
        {
            res["WindowBackgroundBrush"] = Brush(0x16, 0x16, 0x16);
            res["CardBackgroundBrush"] = Brush(0x2D, 0x2D, 0x30);
            res["SelectedCardBackgroundBrush"] = Brush(0x1F, 0x3A, 0x5C);
            res["CardBorderBrush"] = Brush(0x3F, 0x3F, 0x46);
            res["CardOutlineBrush"] = Brush(0x6E, 0x6E, 0x76);
            res["PrimaryTextBrush"] = Brush(0xF0, 0xF0, 0xF0);
            res["SecondaryTextBrush"] = Brush(0xAA, 0xAA, 0xAA);
            // Half a step from SecondaryTextBrush toward the background's opposite - i.e. toward
            // white here, toward black in the light theme - for secondary text that was reading as
            // too washed out. "Darker" literally would mean *less* contrast on a dark background.
            res["SecondaryTextStrongBrush"] = Brush(0xD5, 0xD5, 0xD5);
            res["PanelBackgroundBrush"] = Brush(0x25, 0x25, 0x26);
            res["PanelBorderBrush"] = Brush(0x6E, 0x6E, 0x6E);
            res["ButtonBackgroundBrush"] = Brush(0x3F, 0x3F, 0x46);
            res["InputBackgroundBrush"] = Brush(0x33, 0x33, 0x36);
            res["BoardAreaBrush"] = Brush(0x16, 0x16, 0x16);
            res["AccentTextBrush"] = Brush(0x64, 0xB5, 0xF6);
            // The foot of the button column and the copyright line on dialogs: light enough to read on the dark panels.
            res["FooterTextBrush"] = Brush(0xAA, 0xAA, 0xAA);
            res["FooterLinkBrush"] = Brush(0x64, 0xB5, 0xF6);
            res["DialogCopyrightBrush"] = Brush(0xAA, 0xAA, 0xAA);
            res["ColumnHeaderTextBrush"] = Brushes.White;
            res["AlternateRowBrush"] = Brush(0x20, 0x20, 0x22);
            // The navy behind the button column and the header: deeper in the dark theme so it sits
            // with the near-black board, lighter in the light theme so it isn't a heavy slab beside
            // a pale one. Both keep white text readable (the light one is about 5.4:1).
            res["AccentPanelBrush"] = Brush(0x1F, 0x34, 0x52);
            res["AccentPanelBorderBrush"] = Brush(0x3A, 0x55, 0x80);
            // The title band on dialogs (DialogBand): the same blue, deeper at the left.
            res["DialogBandBrush"] = Gradient(0x12, 0x21, 0x36, 0x1F, 0x34, 0x52);
            // The project name on cards, lighter on the dark cards, and white letters on the cards' half-strength buttons.
            res["CardProjectBrush"] = Brush(0x8F, 0xB4, 0xE0);
            foreach (var key in QuickTextKeys) res[key] = Brushes.White;
            // The button column. Everything in it is a coloured box with its own white text, so its
            // background is free to be whatever sets those boxes off best: the same deep navy here.
            res["SidebarPanelBrush"] = Brush(0x1F, 0x34, 0x52);
            res["SidebarPanelBorderBrush"] = Brush(0x3A, 0x55, 0x80);
            res["SidebarGripBrush"] = Brush(0x8F, 0xA6, 0xC4);
            // The line under each column's name: see-through, so it darkens (or here lightens) whatever
            // colour the column is rather than needing one shade per column.
            res["ColumnHeaderLineBrush"] = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
        }
        else
        {
            res["WindowBackgroundBrush"] = Brush(0xE3, 0xE4, 0xE7);
            res["CardBackgroundBrush"] = Brushes.White;
            res["SelectedCardBackgroundBrush"] = Brush(0xDC, 0xEB, 0xFD);
            res["CardBorderBrush"] = Brush(0xD0, 0xD0, 0xD0);
            res["CardOutlineBrush"] = Brush(0x9E, 0x9E, 0x9E);
            res["PrimaryTextBrush"] = Brush(0x1A, 0x1A, 0x1A);
            res["SecondaryTextBrush"] = Brush(0x88, 0x88, 0x88);
            res["SecondaryTextStrongBrush"] = Brush(0x44, 0x44, 0x44); // 0x88 halved: 50% darker.
            res["PanelBackgroundBrush"] = Brush(0xFA, 0xFA, 0xFA);
            res["PanelBorderBrush"] = Brush(0x33, 0x33, 0x33);
            res["ButtonBackgroundBrush"] = Brush(0xEC, 0xEC, 0xEC);
            res["InputBackgroundBrush"] = Brushes.White;
            res["BoardAreaBrush"] = Brush(0xD4, 0xD4, 0xD8);
            res["AccentTextBrush"] = Brush(0x0B, 0x5F, 0xD9);
            // The foot of the button column (version, About, copyright, Designed in Canada) in a darker grey
            // than SecondaryTextBrush with the standard Windows link blue, and the dialogs' copyright line in slate.
            res["FooterTextBrush"] = Brush(0x66, 0x66, 0x66);
            res["FooterLinkBrush"] = SystemColors.HotTrackBrush;
            res["DialogCopyrightBrush"] = Brush(0x5A, 0x6B, 0x7B);
            res["ColumnHeaderTextBrush"] = Brush(0x1A, 0x1A, 0x1A);
            res["AlternateRowBrush"] = Brush(0xD7, 0xD8, 0xDB);
            res["AccentPanelBrush"] = Brush(0x4B, 0x6D, 0x96);
            res["AccentPanelBorderBrush"] = Brush(0x70, 0x90, 0xB6);
            res["DialogBandBrush"] = Gradient(0x2E, 0x4C, 0x73, 0x4B, 0x6D, 0x96);
            res["CardProjectBrush"] = Brush(0x2E, 0x4C, 0x73);
            // A deep shade of each button's own colour, readable on its half-strength fill.
            res["QuickToDoTextBrush"] = Brush(0x0D, 0x47, 0xA1);
            res["QuickInProgressTextBrush"] = Brush(0x6B, 0x4E, 0x00);
            res["QuickOnHoldTextBrush"] = Brush(0x6E, 0x33, 0x00);
            res["QuickWaitingTextBrush"] = Brush(0x4A, 0x14, 0x8C);
            res["QuickDoneTextBrush"] = Brush(0x1B, 0x5E, 0x20);
            res["QuickDeleteTextBrush"] = Brush(0x8E, 0x1C, 0x1A);
            // Pale, so the slate and blue buttons stand clear of it instead of washing into a navy of
            // much the same strength. The header keeps AccentPanelBrush: its white text needs the depth.
            res["SidebarPanelBrush"] = Brush(0xC1, 0xC9, 0xD4);
            res["SidebarPanelBorderBrush"] = Brush(0x8C, 0x9E, 0xB6);
            res["SidebarGripBrush"] = Brush(0x4F, 0x67, 0x85);
            res["ColumnHeaderLineBrush"] = new SolidColorBrush(Color.FromArgb(0x4D, 0x00, 0x00, 0x00));
        }
    }

    // The letters on a task card's move, done and delete buttons (see QuickToDoBrush and the rest in MainWindow.xaml).
    private static readonly string[] QuickTextKeys =
        ["QuickToDoTextBrush", "QuickInProgressTextBrush", "QuickOnHoldTextBrush", "QuickWaitingTextBrush", "QuickDoneTextBrush", "QuickDeleteTextBrush"];

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    // Left to right, from the first colour to the second.
    private static LinearGradientBrush Gradient(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2) =>
        new(Color.FromRgb(r1, g1, b1), Color.FromRgb(r2, g2, b2), 0);
}
