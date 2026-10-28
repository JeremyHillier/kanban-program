using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using KanbanApp.Models;

namespace KanbanApp.Views;

// The card's background colour, picked on the task screen as well as from the card's right-click menu: a row of
// swatches beside the Force edit box, None first. Each swatch is the theme's own CardColor brush, so it shows the
// shade the card will really have, in light or dark. The colour is saved through SetCardColor, not with the
// other fields (see MainViewModel.Cards.cs), so it is one more step for Undo when it changed.
public partial class AddTaskWindow
{
    public string? SelectedColor { get; private set; }

    private void BuildColourSwatches()
    {
        AddColourSwatch(null);
        foreach (var entry in CardColors.All) AddColourSwatch(entry.Key);
        ShowColour(null);
    }

    private void AddColourSwatch(string? key)
    {
        var swatch = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1) };
        swatch.SetResourceReference(Border.BorderBrushProperty, "CardOutlineBrush");
        if (key is null)
        {
            // None: an empty swatch with a stroke through it.
            swatch.SetResourceReference(Border.BackgroundProperty, "InputBackgroundBrush");
            var stroke = new Line { X1 = 4, Y1 = 14, X2 = 14, Y2 = 4, StrokeThickness = 1.5 };
            stroke.SetResourceReference(Shape.StrokeProperty, "CardOutlineBrush");
            swatch.Child = stroke;
        }
        else
        {
            swatch.SetResourceReference(Border.BackgroundProperty, CardColors.ResourceKey(key));
        }

        var button = new RadioButton
        {
            GroupName = "CardColour",
            Tag = key,
            Content = swatch,
            Margin = new Thickness(3, 0, 0, 0),
            ToolTip = key ?? "No colour",
            IsTabStop = false,
            Cursor = Cursors.Hand,
            Template = (ControlTemplate)FindResource("ColourSwatchTemplate")
        };
        AutomationProperties.SetName(button, $"Colour {key ?? "None"}");
        button.Checked += (_, _) => SelectedColor = key;
        ColourPanel.Children.Add(button);
    }

    // Ticks the swatch for this colour (None for a colour no longer in the list).
    private void ShowColour(string? color)
    {
        var key = CardColors.Get(color)?.Key;
        foreach (var button in ColourPanel.Children.OfType<RadioButton>())
        {
            button.IsChecked = string.Equals(button.Tag as string, key, StringComparison.OrdinalIgnoreCase);
        }
        SelectedColor = key;
    }
}
