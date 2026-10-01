using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace KanbanApp.Views;

// Where every tooltip opens (switched on by the ToolTip style in App.xaml): to the right of the
// pointer, its top level with the pointer's tip, clear of the arrow. Where that would run off the
// right of the screen it goes to the left of the pointer instead, and near the bottom it sits above
// the pointer's level.
//
// Done here rather than with Placement="MousePoint": the built-in placements follow Windows' menu
// alignment, which on a PC set up for right-handed pen use opens every tooltip to the LEFT of the
// pointer, over the very thing being pointed at.
public static class ToolTipPlacement
{
    // Clear of the arrow pointer, which is about 12 x 19 at 100% and grows with the screen scaling.
    internal const double GapRight = 20;
    internal const double GapLeft = 4;

    public static readonly DependencyProperty BesidePointerProperty = DependencyProperty.RegisterAttached(
        "BesidePointer", typeof(bool), typeof(ToolTipPlacement), new PropertyMetadata(false, OnBesidePointerChanged));

    public static bool GetBesidePointer(ToolTip tip) => (bool)tip.GetValue(BesidePointerProperty);
    public static void SetBesidePointer(ToolTip tip, bool value) => tip.SetValue(BesidePointerProperty, value);

    private static void OnBesidePointerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToolTip tip) return;

        if (e.NewValue is true)
        {
            tip.Placement = PlacementMode.Custom;
            tip.CustomPopupPlacementCallback = (popupSize, _, _) => Place(tip, popupSize);
        }
        else
        {
            tip.ClearValue(ToolTip.PlacementProperty);
            tip.ClearValue(ToolTip.CustomPopupPlacementCallbackProperty);
        }
    }

    // WPF asks in the coordinates of the element the tooltip belongs to, measured in device pixels.
    // The pointer is read from Windows (WPF's own idea of it is stale whenever the pointer last moved
    // over another window) and turned into the element's units, then scaled to device pixels.
    private static CustomPopupPlacement[] Place(ToolTip tip, Size popupSize)
    {
        if (tip.PlacementTarget is not { } target || PresentationSource.FromVisual(target) is null || !GetCursorPos(out var screen)) return [];

        var dpi = VisualTreeHelper.GetDpi(target);
        var pointer = target.PointFromScreen(new Point(screen.X, screen.Y));

        // Opened from the keyboard (Tab onto a control), the pointer can be anywhere: under the control
        // then, or above it at the bottom of the screen. Bounds rather than IsMouseOver, which a
        // disabled control (Undo with nothing to undo) never reports.
        if (target is FrameworkElement element && !new Rect(0, 0, element.ActualWidth, element.ActualHeight).Contains(pointer))
        {
            var height = element.ActualHeight * dpi.DpiScaleY;
            return
            [
                new(new Point(0, height), PopupPrimaryAxis.Horizontal),
                new(new Point(0, -popupSize.Height), PopupPrimaryAxis.Horizontal),
                new(new Point(0, 0), PopupPrimaryAxis.Horizontal), // over a control too tall for either
            ];
        }

        return Placements(new Point(pointer.X * dpi.DpiScaleX, pointer.Y * dpi.DpiScaleY), popupSize, dpi.DpiScaleX);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    // First choice right of the pointer, then left of it; near the bottom of the screen the same two
    // again, ending level with the pointer's tip instead of starting there. WPF takes the first that
    // fits on screen (when none does, it falls back to a corner of the screen, hence all four).
    internal static CustomPopupPlacement[] Placements(Point pointer, Size popupSize, double scale)
    {
        var right = pointer.X + GapRight * scale;
        var left = pointer.X - GapLeft * scale - popupSize.Width;
        var above = pointer.Y - popupSize.Height;
        return
        [
            new(new Point(right, pointer.Y), PopupPrimaryAxis.Horizontal),
            new(new Point(left, pointer.Y), PopupPrimaryAxis.Horizontal),
            new(new Point(right, above), PopupPrimaryAxis.Horizontal),
            new(new Point(left, above), PopupPrimaryAxis.Horizontal),
        ];
    }
}
