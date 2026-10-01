using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KanbanApp.ViewModels;

namespace KanbanApp;

// The due-date calendar: a month at a time. Clicking a card's due date opens it in a popup under
// the date; in the right-click menus it is the Set Due Date submenu itself, so it slides out to the
// right on hover with no extra click (one card, or every selected card). Clicking a day sets it and
// closes; Today and Clear Due Date do what they say; Esc or a click elsewhere closes without
// changing anything.
public partial class MainWindow
{
    // The calendar and its buttons. close is whatever closes the popup or menu it sits in; it runs
    // first, and the cards change afterwards on the dispatcher, because changing the cards while a
    // popup is still closing deadlocks WPF's layout (the same pattern as every quick-edit menu).
    private FrameworkElement BuildDueDateCalendar(IReadOnlyList<CardViewModel> cards, MainViewModel viewModel, Action close)
    {
        // The date they all share, if they share one; otherwise the calendar opens on today with nothing picked.
        var shared = cards.Select(c => c.DueDate).Distinct().Count() == 1 ? cards[0].DueDate : null;

        var calendar = new Calendar
        {
            DisplayMode = CalendarMode.Month,
            SelectionMode = CalendarSelectionMode.SingleDate,
            SelectedDate = shared,
            DisplayDate = shared ?? DateTime.Today,
            Margin = new Thickness(6, 6, 6, 0)
        };
        Services.CalendarWheelSupport.Attach(calendar); // the wheel moves between months

        var todayButton = new Button { Content = "Today", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6, 4, 4, 6) };
        var clearButton = new Button { Content = "Clear Due Date", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(4, 4, 6, 6) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(todayButton);
        buttons.Children.Add(clearButton);

        var panel = new StackPanel();
        if (cards.Count > 1)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Due date for {cards.Count} tasks", FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 8, 8, 0), Foreground = Brushes.Black
            });
        }
        panel.Children.Add(calendar);
        panel.Children.Add(buttons);

        void Apply(DateTime? date) => Dispatcher.BeginInvoke(new Action(() =>
        {
            close();
            if (cards.Count == 1) viewModel.SetCardDueDate(cards[0], date);
            else viewModel.SetCardsDueDate(cards, date);
        }), DispatcherPriority.Background);

        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is { } picked && picked.Date != shared?.Date) Apply(picked.Date);
        };
        // Clicking the day already picked raises no change; it still means "that date, close".
        calendar.PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (Mouse.DirectlyOver is FrameworkElement { DataContext: DateTime clicked } && clicked.Date == shared?.Date) Apply(clicked.Date);
        };
        todayButton.Click += (_, _) => Apply(DateTime.Today);
        clearButton.Click += (_, _) => Apply(null);
        panel.PreviewKeyDown += (_, keyArgs) =>
        {
            if (keyArgs.Key != Key.Escape) return;
            close();
            keyArgs.Handled = true;
        };

        // The stock calendar is light in both themes, hence the fixed white background and black text.
        return new Border
        {
            Background = Brushes.White, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Child = panel
        };
    }

    // The calendar takes mouse capture on a click and can keep it after its popup window is gone,
    // which swallows every later click until something resets it. Release both the managed and the
    // Win32 capture, every time a calendar closes.
    private void ReleaseCalendarCapture()
    {
        Mouse.Capture(null);
        NativeMethods.ReleaseCapture();
        Keyboard.Focus(this);
    }

    // Clicking a card's due date: the calendar in a popup under it.
    private void ShowDueDateCalendar(FrameworkElement placementTarget, IReadOnlyList<CardViewModel> cards, MainViewModel viewModel)
    {
        if (cards.Count == 0) return;

        // StaysOpen = true, closed by hand: WPF's own click-outside dismissal races the calendar's
        // internal popups and left the app with a phantom mouse capture (see ReleaseCalendarCapture).
        var popup = new Popup
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Bottom,
            StaysOpen = true,
            AllowsTransparency = true
        };

        MouseButtonEventHandler onOutsideClick = null!;
        onOutsideClick = (_, _) => ClosePopup();

        void ClosePopup()
        {
            PreviewMouseDown -= onOutsideClick;
            Deactivated -= OnDeactivatedClosePopup;
            popup.IsOpen = false;
            ReleaseCalendarCapture();
        }

        void OnDeactivatedClosePopup(object? _, EventArgs __) => ClosePopup();

        var content = BuildDueDateCalendar(cards, viewModel, ClosePopup);
        popup.Child = content;
        popup.Opened += (_, _) =>
        {
            content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            PreviewMouseDown += onOutsideClick;
            Deactivated += OnDeactivatedClosePopup;
        };

        popup.IsOpen = true;
    }

    // The right-click menus: "Set Due Date" is a submenu whose only item is the calendar, so it
    // opens to the right on hover like Move To or Priority. The item keeps the menu open while the
    // calendar is clicked around in (StaysOpenOnClick) and draws no highlight or check column of its
    // own - a bare ContentPresenter template - so it looks like the calendar, not a menu entry.
    private void AddDueDateSubmenu(ContextMenu menu, string header, IReadOnlyList<CardViewModel> cards, MainViewModel viewModel)
    {
        var submenu = AddSubmenu(menu, header);

        void CloseMenu()
        {
            menu.IsOpen = false;
            ReleaseCalendarCapture();
        }

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        var host = new MenuItem
        {
            Header = BuildDueDateCalendar(cards, viewModel, CloseMenu),
            StaysOpenOnClick = true,
            Focusable = false,
            Template = new ControlTemplate(typeof(MenuItem)) { VisualTree = presenter }
        };
        submenu.Items.Add(host);
    }
}
