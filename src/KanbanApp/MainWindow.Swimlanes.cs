using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.ViewModels;

namespace KanbanApp;

// The Swim lanes button's menu, lane headers, and keeping the board's scrolling right for either layout.
public partial class MainWindow
{
    // The real workflow column behind whatever the template was given: a swimlane's column stands for it.
    internal static ColumnViewModel RealColumn(ColumnViewModel column) => (column as LaneColumnViewModel)?.Source ?? column;

    // The plain board is as tall as the window and its columns scroll inside; swimlanes are taller than the
    // window and the whole board scrolls down.
    private void ApplyBoardScroll()
    {
        BoardScrollViewer.VerticalScrollBarVisibility = DataContext is MainViewModel { IsSwimlanesOn: true } ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        PassBoardWidth();
    }

    // The columns share out the board's width, so in lane mode room is kept for the vertical scroll bar that the
    // lanes bring, or the last column would run under it and a horizontal scroll bar would appear.
    private void PassBoardWidth()
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SetBoardWidth(BoardScrollViewer.ActualWidth - (viewModel.IsSwimlanesOn ? SystemParameters.VerticalScrollBarWidth : 0));
    }

    private void Swimlanes_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement target || DataContext is not MainViewModel viewModel) return;
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        // A menu choice is applied after the menu has closed (AddMenuItem), a moment after the click. A right-click on
        // the button in that moment must win: it clears the lanes, and a choice still waiting is dropped, or it would
        // switch the lanes straight back on and the clear would seem not to have worked.
        var version = _laneChoiceVersion;

        void Choice(string header, SwimlaneMode mode) =>
            AddMenuItem(menu, header, () => { if (version == _laneChoiceVersion) viewModel.SetSwimlaneMode(mode); }, isChecked: viewModel.SwimlaneMode == mode);

        Choice("No Swimlanes", SwimlaneMode.Off);
        Choice("By Project", SwimlaneMode.Project);
        Choice("By Goal", SwimlaneMode.Goal);
        Choice("By Assigned To", SwimlaneMode.AssignedTo);
        menu.Items.Add(new Separator());
        AddMenuItem(menu, "Expand All Lanes", () => viewModel.SetAllLanesExpanded(true), isEnabled: viewModel.IsSwimlanesOn);
        AddMenuItem(menu, "Collapse All Lanes", () => viewModel.SetAllLanesExpanded(false), isEnabled: viewModel.IsSwimlanesOn);
        menu.IsOpen = true;
    }

    // Right-click on the button clears the lanes: back to the plain board. It is done as the button is pressed, and the
    // release is swallowed too, so one click clears them however the click arrives.
    private int _laneChoiceVersion;

    private void Swimlanes_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _laneChoiceVersion++; // drops a menu choice that has been clicked but not yet applied
        if (DataContext is MainViewModel viewModel) viewModel.SetSwimlaneMode(SwimlaneMode.Off);
        e.Handled = true;
    }

    private void Swimlanes_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void LaneHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LaneViewModel lane }) return;
        lane.IsExpanded = !lane.IsExpanded;
        e.Handled = true;
    }
}
