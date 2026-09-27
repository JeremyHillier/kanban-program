using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KanbanApp.Views;

// The Dashboard's colours. Chosen and checked with a palette validator for colour-blind separation
// and contrast against the chart cards, light and dark separately (dark is its own set of steps,
// not a flipped copy):
//   - statuses: one hue each, in board order; On Hold is magenta rather than the board's orange,
//     which sits too close to In Progress yellow to tell apart in a stacked bar
//   - priorities: red / amber / neutral grey / blue, like the board's priority badges
//   - due dates: red and orange for overdue and today, then one blue getting quieter with distance
//   - time since last update: one blue getting stronger with age, so stale work stands out
// Text never takes a series colour - numbers and labels stay in the theme's text colours.
internal sealed class DashboardPalette(bool dark)
{
    private static SolidColorBrush B(uint rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        brush.Freeze();
        return brush;
    }

    public Brush Status(string columnName) => columnName switch
    {
        "To Do" => dark ? B(0x3987e5) : B(0x2a78d6),
        "In Progress" => dark ? B(0xc98500) : B(0xeda100),
        "On Hold" => dark ? B(0xd55181) : B(0xe87ba4),
        "Waiting" => dark ? B(0x9085e9) : B(0x4a3aa7),
        "Done" => B(0x008300),
        _ => Neutral
    };

    public Brush Priority(string priority) => priority switch
    {
        "High" => B(0xd03b3b),
        "Medium" => dark ? B(0xc98500) : B(0xeda100),
        "Low" => dark ? B(0x3987e5) : B(0x2a78d6),
        _ => Neutral
    };

    public Brush Neutral => dark ? B(0x6f6e69) : B(0x8a8984);
    public Brush Series => dark ? B(0x3987e5) : B(0x2a78d6);

    // Overdue, Today, Next 7 days, 8-30 days, Later, No due date.
    public IReadOnlyList<Brush> DueDates =>
    [
        B(0xd03b3b), B(0xec835a),
        dark ? B(0x86b6ef) : B(0x1c5cab), B(0x3987e5), dark ? B(0x1c5cab) : B(0x86b6ef),
        Neutral
    ];

    // Under a week ... over 3 months.
    public IReadOnlyList<Brush> LastUpdated => dark
        ? [B(0x1c5cab), B(0x3987e5), B(0x6da7ec), B(0x9ec5f4)]
        : [B(0x86b6ef), B(0x5598e7), B(0x256abf), B(0x104281)];
}

// Builds the Dashboard's charts as plain WPF elements. Marks are thin with a 2px gap of card
// surface between stacked pieces, the data end of each bar is rounded and the baseline end square,
// every bar and piece has a tooltip, and a legend is shown whenever a chart has more than one series.
internal static class DashboardCharts
{
    public const double Gap = 2;

    public sealed record Segment(string Series, int Count, Brush Brush);

    public sealed record Bar(string Label, IReadOnlyList<Segment> Segments, string? ToolTip = null)
    {
        public int Total => Segments.Sum(s => s.Count);
    }

    private static TextBlock Text(string text, double size, string brushKey, FontWeight? weight = null)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }

    // A chart's card: title, an optional line under it, an optional legend, then the chart.
    public static Border Card(string title, string? subtitle, UIElement chart, UIElement? legend = null)
    {
        var body = new StackPanel();
        body.Children.Add(Text(title, 14, "PrimaryTextBrush", FontWeights.SemiBold));
        if (subtitle is not null)
        {
            var sub = Text(subtitle, 11, "SecondaryTextBrush");
            sub.TextWrapping = TextWrapping.Wrap;
            sub.Margin = new Thickness(0, 2, 0, 0);
            body.Children.Add(sub);
        }
        if (legend is not null)
        {
            ((FrameworkElement)legend).Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(legend);
        }
        ((FrameworkElement)chart).Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(chart);

        var card = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(16, 14, 16, 14), Child = body };
        card.SetResourceReference(Border.BackgroundProperty, "PanelBackgroundBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        return card;
    }

    public static WrapPanel Legend(IEnumerable<(string Name, Brush Brush)> items)
    {
        var panel = new WrapPanel();
        foreach (var (name, brush) in items)
        {
            var entry = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 2) };
            entry.Children.Add(new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), Background = brush, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            entry.Children.Add(Text(name, 11, "SecondaryTextBrush"));
            panel.Children.Add(entry);
        }
        return panel;
    }

    // Vertical bars, stacked when a bar has several segments (the first segment sits on the
    // baseline). labelEvery thins the axis labels on a crowded chart; showTotal picks which bars
    // carry their number on top (default: all) - the tooltip always has it.
    public static FrameworkElement Columns(IReadOnlyList<Bar> bars, double plotHeight = 150, double barWidth = 36,
        int labelEvery = 1, Func<int, bool>? showTotal = null)
    {
        var max = Math.Max(1, bars.Count == 0 ? 0 : bars.Max(b => b.Total));
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // totals
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plotHeight) }); // plot
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });         // baseline
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // labels

        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // The whole column is the hover target, not just the (possibly tiny) bar.
            var hit = new Border { Background = Brushes.Transparent, ToolTip = bar.ToolTip ?? DefaultToolTip(bar) };
            Grid.SetColumn(hit, i);
            Grid.SetRowSpan(hit, 4);
            grid.Children.Add(hit);

            if (showTotal?.Invoke(i) ?? true)
            {
                var total = Text(bar.Total.ToString(), 12, "PrimaryTextBrush", FontWeights.SemiBold);
                total.HorizontalAlignment = HorizontalAlignment.Center;
                total.Margin = new Thickness(0, 0, 0, 3);
                total.IsHitTestVisible = false;
                Grid.SetColumn(total, i);
                grid.Children.Add(total);
            }

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
            var pieces = bar.Segments.Where(s => s.Count > 0).ToList();
            for (var s = pieces.Count - 1; s >= 0; s--) // top piece first
            {
                var piece = pieces[s];
                var isTop = s == pieces.Count - 1;
                var height = Math.Max(2, piece.Count / (double)max * plotHeight - (isTop ? 0 : Gap));
                stack.Children.Add(new Border
                {
                    Width = barWidth, Height = height, Background = piece.Brush,
                    CornerRadius = isTop ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(0),
                    Margin = new Thickness(0, 0, 0, s == 0 ? 0 : Gap)
                });
            }
            Grid.SetColumn(stack, i);
            Grid.SetRow(stack, 1);
            grid.Children.Add(stack);

            if (i % labelEvery == (bars.Count - 1) % labelEvery) // always label the last bar
            {
                var label = Text(bar.Label, 11, "SecondaryTextBrush");
                label.TextAlignment = TextAlignment.Center;
                label.TextWrapping = TextWrapping.Wrap;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.Margin = new Thickness(2, 5, 2, 0);
                label.IsHitTestVisible = false;
                Grid.SetColumn(label, i);
                Grid.SetRow(label, 3);
                grid.Children.Add(label);
            }
        }

        var baseline = new Border { IsHitTestVisible = false };
        baseline.SetResourceReference(Border.BackgroundProperty, "CardBorderBrush");
        Grid.SetRow(baseline, 2);
        Grid.SetColumnSpan(baseline, Math.Max(1, bars.Count));
        grid.Children.Add(baseline);
        return grid;
    }

    // Horizontal bars, one row per item, stacked left to right, the number just past the bar's end.
    // Bars share one scale, so their lengths compare directly.
    public static FrameworkElement Rows(IReadOnlyList<Bar> bars, double labelWidth = 170, double barHeight = 14)
    {
        var max = Math.Max(1, bars.Count == 0 ? 0 : bars.Max(b => b.Total));
        var list = new StackPanel();
        foreach (var bar in bars)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6), Background = Brushes.Transparent, ToolTip = bar.ToolTip ?? DefaultToolTip(bar) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = Text(bar.Label, 12, "PrimaryTextBrush");
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(label);

            // Each piece takes a star share equal to its count; the rest of the scale is empty space,
            // and a fixed-width slot right after the bar holds the number.
            var track = new Grid();
            Grid.SetColumn(track, 1);
            var pieces = bar.Segments.Where(s => s.Count > 0).ToList();
            for (var s = 0; s < pieces.Count; s++)
            {
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(pieces[s].Count, GridUnitType.Star) });
                var isEnd = s == pieces.Count - 1;
                var piece = new Border
                {
                    Height = barHeight, Background = pieces[s].Brush,
                    CornerRadius = isEnd ? new CornerRadius(0, 4, 4, 0) : new CornerRadius(0),
                    Margin = new Thickness(0, 0, isEnd ? 0 : Gap, 0)
                };
                Grid.SetColumn(piece, s);
                track.Children.Add(piece);
            }
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            var value = Text(bar.Total.ToString(), 12, "PrimaryTextBrush", FontWeights.SemiBold);
            value.Margin = new Thickness(6, 0, 0, 0);
            value.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(value, pieces.Count);
            track.Children.Add(value);
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, max - bar.Total), GridUnitType.Star) });

            row.Children.Add(track);
            list.Children.Add(row);
        }
        return list;
    }

    // "In Progress: 17 - High 4, Normal 13" (pieces with none are left out).
    private static string DefaultToolTip(Bar bar)
    {
        var pieces = bar.Segments.Where(s => s.Count > 0).ToList();
        if (pieces.Count <= 1) return $"{bar.Label}: {bar.Total}";
        return $"{bar.Label}: {bar.Total}\n" + string.Join("\n", pieces.Select(p => $"   {p.Series}: {p.Count}"));
    }
}
