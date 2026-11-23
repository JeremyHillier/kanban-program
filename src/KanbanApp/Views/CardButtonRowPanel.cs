using System.Windows;
using System.Windows.Controls;

namespace KanbanApp.Views;

// The row of buttons along the foot of a card (the move buttons, then Done and Delete). Only the buttons
// showing take part: they go in as few rows as the width allows, split by CardButtonLayout.SplitRows, so a
// narrow column gets even rows and never a button hanging off the edge. StartsGroup marks the first button of
// the second group (Done).
public sealed class CardButtonRowPanel : Panel
{
    public static readonly DependencyProperty StartsGroupProperty = DependencyProperty.RegisterAttached(
        "StartsGroup", typeof(bool), typeof(CardButtonRowPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static bool GetStartsGroup(UIElement element) => (bool)element.GetValue(StartsGroupProperty);

    public static void SetStartsGroup(UIElement element, bool value) => element.SetValue(StartsGroupProperty, value);

    // The space between rows.
    public double RowGap { get; set; } = 3;

    private List<UIElement> _shown = [];
    private IReadOnlyList<int> _rows = [];
    private double _rowHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        var shown = new List<UIElement>();
        var groupStart = -1;
        var reachedGroup = false;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (GetStartsGroup(child)) reachedGroup = true;
            if (child.Visibility == Visibility.Collapsed) continue;
            if (reachedGroup && groupStart < 0) groupStart = shown.Count;
            shown.Add(child);
        }

        _shown = shown;
        if (shown.Count == 0)
        {
            _rows = [];
            return new Size(0, 0);
        }

        var slot = shown.Max(c => c.DesiredSize.Width);
        _rowHeight = shown.Max(c => c.DesiredSize.Height);
        var perRow = double.IsInfinity(availableSize.Width) || slot <= 0 ? shown.Count : (int)(availableSize.Width / slot);
        _rows = CardButtonLayout.SplitRows(shown.Count, perRow, groupStart);

        var widest = 0.0;
        var index = 0;
        foreach (var size in _rows)
        {
            widest = Math.Max(widest, shown.Skip(index).Take(size).Sum(c => c.DesiredSize.Width));
            index += size;
        }
        return new Size(widest, _rows.Count * _rowHeight + (_rows.Count - 1) * RowGap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        var index = 0;
        foreach (var size in _rows)
        {
            var x = 0.0;
            for (var i = 0; i < size; i++, index++)
            {
                var child = _shown[index];
                child.Arrange(new Rect(x, y, child.DesiredSize.Width, _rowHeight));
                x += child.DesiredSize.Width;
            }
            y += _rowHeight + RowGap;
        }
        return finalSize;
    }
}
