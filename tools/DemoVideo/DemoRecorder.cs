// Records the demo video's frames. Lives in tools/DemoVideo and is NOT part of the app: to use it, it
// is copied into the project root and hooked into App.xaml.cs for one run, then taken out again
// (see README.md). It plays a scripted scene inside the real app, against a made-up board in a
// throwaway folder, and saves PNG frames plus a manifest of durations for the Encoder. The mouse
// pointer, captions and the dragged "ghost" card are drawn in; everything else is the app's own screens.
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

internal static class DemoRecorder
{
    private const double W = 1600, H = 900, Scale = 1.2;   // 1920x1080 output; wide enough for all five columns
    private const int MoveFrameMs = 50;                      // 20 frames a second while things move
    private const double Pace = 1.25;                        // one dial for the whole video: holds, pointer moves and typing all stretch by this
    private static readonly Color Navy = Color.FromRgb(0x1E, 0x3A, 0x5F);

    private static string _framesDir = "";
    private static readonly List<string> Manifest = [];
    private static int _frameNo;

    private static MainWindow _main = null!;
    private static FrameworkElement _mainContent = null!;
    private static BitmapSource? _mainShot;
    private static Window? _dialog;
    private static BitmapSource? _dialogShot;
    private static Rect _dialogRect;
    private static string _caption = "";
    private static Point _cursor = new(900, 520);
    private static BitmapSource? _ghost;
    private static Point _ghostOffset;
    private static double _pulse;                            // 0 = none, else ring radius

    private static async Task Idle(int ms = 250) { await Task.Delay(ms); await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Walk(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    private static BitmapSource Shoot(FrameworkElement e, Brush background)
    {
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth * Scale), (int)Math.Ceiling(e.ActualHeight * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(background, null, new Rect(0, 0, e.ActualWidth, e.ActualHeight));
            dc.DrawRectangle(new VisualBrush(e) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, e.ActualWidth, e.ActualHeight));
        }
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    private static Brush WindowBrush => (Brush)Application.Current.Resources["WindowBackgroundBrush"];

    private static async Task Refresh()
    {
        await Idle(180);
        _mainShot = Shoot(_mainContent, WindowBrush);
        if (_dialog is not null) _dialogShot = Shoot((FrameworkElement)_dialog.Content, WindowBrush);
    }

    private static FormattedText Text(string text, double size, Brush brush, FontWeight weight, double maxWidth = 1200) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal), size, brush, 1.0)
        { MaxTextWidth = maxWidth, TextAlignment = TextAlignment.Center };

    private static void Emit(int durationMs, Action<DrawingContext>? custom = null)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            if (custom is not null) custom(dc);
            else
            {
                dc.DrawImage(_mainShot, new Rect(0, 0, W, H));

                if (_dialog is not null && _dialogShot is not null)
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0)), null, new Rect(0, 0, W, H));
                    const double pad = 16; // the window's own margin, which a picture of its content leaves out
                    var frame = new Rect(_dialogRect.X - pad, _dialogRect.Y - pad - 30, _dialogRect.Width + pad * 2, _dialogRect.Height + pad * 2 + 30);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), null, new Rect(frame.X + 6, frame.Y + 8, frame.Width, frame.Height), 8, 8);
                    dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70)), 1), frame, 6, 6);
                    var title = Text(_dialog.Title, 12.5, Brushes.Black, FontWeights.Normal, 400);
                    title.TextAlignment = TextAlignment.Left;
                    dc.DrawText(title, new Point(frame.X + 12, frame.Y + 7));
                    var close = Text("✕", 12, Brushes.DimGray, FontWeights.Normal, 30);
                    dc.DrawText(close, new Point(frame.Right - 38, frame.Y + 7));
                    dc.DrawRectangle(WindowBrush, null, new Rect(frame.X + 1, frame.Y + 30, frame.Width - 2, frame.Height - 31));
                    dc.DrawImage(_dialogShot, _dialogRect);
                }

                if (_ghost is not null)
                {
                    dc.PushOpacity(0.85);
                    var r = new Rect(_cursor.X - _ghostOffset.X, _cursor.Y - _ghostOffset.Y, _ghost.PixelWidth / Scale, _ghost.PixelHeight / Scale);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)), null, new Rect(r.X + 5, r.Y + 7, r.Width, r.Height), 6, 6);
                    dc.DrawImage(_ghost, r);
                    dc.Pop();
                }

                if (_caption.Length > 0)
                {
                    var text = Text(_caption, 22, Brushes.White, FontWeights.SemiBold, 1100);
                    var barHeight = text.Height + 24;
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xEB, Navy.R, Navy.G, Navy.B)), null, new Rect(0, H - barHeight, W, barHeight));
                    dc.DrawText(text, new Point((W - 1100) / 2, H - barHeight + 12));
                }

                if (_pulse > 0)
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xC1, 0x07)), new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xA0, 0x00)), 2.5), _cursor, _pulse, _pulse);
                }

                DrawCursor(dc, _cursor);
            }
        }

        var bmp = new RenderTargetBitmap((int)(W * Scale), (int)(H * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var name = $"f{_frameNo++:0000}.png";
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using (var fs = File.Create(Path.Combine(_framesDir, name))) encoder.Save(fs);
        Manifest.Add($"{name}|{durationMs}");
    }

    private static void DrawCursor(DrawingContext dc, Point p)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(p, true, true);
            g.LineTo(new Point(p.X, p.Y + 21), true, false);
            g.LineTo(new Point(p.X + 5, p.Y + 16.5), true, false);
            g.LineTo(new Point(p.X + 9, p.Y + 25), true, false);
            g.LineTo(new Point(p.X + 12.5, p.Y + 23.5), true, false);
            g.LineTo(new Point(p.X + 8.5, p.Y + 15.5), true, false);
            g.LineTo(new Point(p.X + 15, p.Y + 15.5), true, false);
        }
        dc.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.3) { LineJoin = PenLineJoin.Round }, geometry);
    }

    private static void Hold(int ms) => Emit((int)(ms * Pace));

    private static void MoveTo(Point target, int ms = 600)
    {
        var from = _cursor;
        var steps = Math.Max(2, (int)(ms * Pace) / MoveFrameMs);
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            t = t * t * (3 - 2 * t); // ease in and out
            _cursor = new Point(from.X + (target.X - from.X) * t, from.Y + (target.Y - from.Y) * t);
            Emit(MoveFrameMs);
        }
    }

    private static void Click()
    {
        foreach (var radius in new[] { 8.0, 15, 22 }) { _pulse = radius; Emit(70); }
        _pulse = 0;
    }

    private static Point CentreOnMain(FrameworkElement e, double fx = 0.5, double fy = 0.5) =>
        e.TranslatePoint(new Point(e.ActualWidth * fx, e.ActualHeight * fy), _mainContent);

    private static Button SidebarButton(string startsWith) =>
        Walk(_main).OfType<Button>().First(b => Walk(b).OfType<TextBlock>().Any(t => t.Text.StartsWith(startsWith, StringComparison.Ordinal)));

    private static Border CardBorder(CardViewModel card) =>
        Walk(_main).OfType<Border>().First(b => b.DataContext == card && VisualTreeHelper.GetParent(b) is ContentPresenter);

    private static FrameworkElement ColumnArea(MainViewModel vm, string name)
    {
        var column = vm.Columns.Single(c => c.Name == name);
        return Walk(_main).OfType<FrameworkElement>().Where(e => e.DataContext == column && e.ActualHeight > 300).OrderByDescending(e => e.ActualHeight * e.ActualWidth).First();
    }

    private static void TitleCard(string big, string small, int ms)
    {
        Emit((int)(ms * Pace), dc =>
        {
            dc.DrawRectangle(new SolidColorBrush(Navy), null, new Rect(0, 0, W, H));
            var top = Text(big, 54, Brushes.White, FontWeights.Bold);
            dc.DrawText(top, new Point((W - 1200) / 2, H / 2 - top.Height - 6));
            var bottom = Text(small, 24, new SolidColorBrush(Color.FromRgb(0xC0, 0xCB, 0xDA)), FontWeights.Normal);
            dc.DrawText(bottom, new Point((W - 1200) / 2, H / 2 + 18));
        });
    }

    private static async Task Drag(MainViewModel vm, CardViewModel card, string toColumn)
    {
        var border = CardBorder(card);
        var grab = CentreOnMain(border, 0.5, 0.35);
        MoveTo(grab, 700);
        Hold(250);
        _ghost = Shoot(border, (Brush)Application.Current.Resources["CardBackgroundBrush"]);
        _ghostOffset = new Point(border.ActualWidth * 0.5, border.ActualHeight * 0.35);
        _pulse = 10; Emit(120); _pulse = 0;

        var area = ColumnArea(vm, toColumn);
        var drop = CentreOnMain(area, 0.5, 0.0);
        drop.Y += 150;
        MoveTo(drop, 1100);
        Hold(300);

        vm.MoveCardCommand.Execute((card, vm.Columns.Single(c => c.Name == toColumn)));
        _ghost = null;
        await Refresh();
        Hold(1300);
    }

    public static async void Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kanban-demo");
        var log = new List<string>();
        try
        {
            _framesDir = Path.Combine(dir, $"frames-{DateTime.Now:HHmmss}");
            Directory.CreateDirectory(_framesDir);
            UpdateChecker.FetchOverride = _ => Task.FromResult<UpdateInfo?>(null); // stay offline

            var dataDir = Path.Combine(dir, $"data-{DateTime.Now:HHmmss}");
            Directory.CreateDirectory(dataDir);
            var db = new DatabaseService(Path.Combine(dataDir, "demo.db"));
            db.SetSetting("LastSeenVersion", "v999"); // no What's New over the demo
            db.SetSetting("ShowWhatsNew", "False");
            _main = new MainWindow(db) { Width = W + 16, Height = H + 39, WindowStartupLocation = WindowStartupLocation.Manual, Left = 40, Top = 40 };
            _main.Show();
            var vm = (MainViewModel)_main.DataContext;
            if (vm.IsDarkMode) vm.ToggleTheme();
            await Idle(600);
            foreach (var w in _main.OwnedWindows.OfType<Window>().ToList()) w.Close();
            _mainContent = (FrameworkElement)_main.Content;
            _main.Width += W - _mainContent.ActualWidth;
            _main.Height += H - _mainContent.ActualHeight;
            await Idle(400);
            log.Add($"content {_mainContent.ActualWidth}x{_mainContent.ActualHeight}");
            foreach (var badge in Walk(_main).OfType<TextBlock>().Where(t => t.Text == "TEST BUILD")) badge.Visibility = Visibility.Collapsed; // the released app has no badge
            vm.SetFitColumnsToWindow(false); // the recording is laid out for this exact width
            vm.SetColumnWidth(232);

            // A made-up board.
            foreach (var p in new[] { "Website Redesign", "Client Onboarding", "Office Move" }) vm.AddProject(p);
            vm.AddPerson("Alice"); vm.AddPerson("Ben");
            ProjectViewModel Project(string n) => vm.Projects.Single(p => p.Name == n);
            PersonViewModel Person(string n) => vm.People.Single(p => p.Name == n);
            ColumnViewModel Column(string n) => vm.Columns.Single(c => c.Name == n);
            var today = DateTime.Today;
            vm.AddCard("Draft the homepage copy", Column("To Do"), Project("Website Redesign"), "High", today, Person("Alice"), false, null, null);
            vm.AddCard("Send the welcome pack", Column("To Do"), Project("Client Onboarding"), "Medium", today, Person("Ben"), false, null, null);
            vm.AddCard("Book the moving company", Column("On Hold"), Project("Office Move"), "Normal", today.AddDays(5), null, false, null, null);
            vm.AddCard("Design the new logo", Column("In Progress"), Project("Website Redesign"), "High", today.AddDays(1), Person("Alice"), false, null, null);
            vm.AddCard("Set up the client portal", Column("In Progress"), Project("Client Onboarding"), "Normal", today.AddDays(3), Person("Ben"), false, null, null);
            vm.AddCard("Sign the office lease", Column("Done"), Project("Office Move"), "High", today.AddDays(-2), null, false, null, null);
            await Refresh();

            // ---- Opening ----
            TitleCard("Kanban Task Board", "The basics in under a minute", 2600);
            _caption = "Your tasks, laid out in columns from To Do to Done";
            Hold(2600);

            // ---- 1. Create a task ----
            _caption = "To add a task, click New Task";
            Hold(900);
            MoveTo(CentreOnMain(SidebarButton("New Task")), 900);
            Click();

            _dialog = new AddTaskWindow(vm) { Owner = _main, WindowStartupLocation = WindowStartupLocation.Manual, Left = 60, Top = 60, ShowActivated = false };
            _dialog.Show();
            await Idle(700);
            var dialogContent = (FrameworkElement)_dialog.Content;
            var fit = Math.Min(1.0, (H - 150) / dialogContent.ActualHeight);
            _dialogRect = new Rect((W - dialogContent.ActualWidth * fit) / 2, 44 + (H - 120 - dialogContent.ActualHeight * fit) / 2, dialogContent.ActualWidth * fit, dialogContent.ActualHeight * fit);
            log.Add($"dialog {dialogContent.ActualWidth}x{dialogContent.ActualHeight} fit {fit:0.00}");
            // With the dialog drawn smaller than life, positions inside it shrink by the same amount.
            Point OnDialog(FrameworkElement e, double fx = 0.5, double fy = 0.5)
            {
                var p = e.TranslatePoint(new Point(e.ActualWidth * fx, e.ActualHeight * fy), dialogContent);
                return new Point(_dialogRect.X + p.X * fit, _dialogRect.Y + p.Y * fit);
            }
            await Refresh();
            Hold(900);

            _caption = "Pick a project and type what needs doing";
            var projectBox = (ComboBox)_dialog.FindName("ProjectComboBox");
            var initialProject = projectBox.SelectedItem;
            MoveTo(OnDialog(projectBox, 0.3), 700);
            Click();
            projectBox.SelectedItem = projectBox.Items.Cast<object>().First(i => (i as ProjectViewModel)?.Name == "Website Redesign");
            await Refresh();
            Hold(700);

            var details = (TextBox)_dialog.FindName("DetailsTextBox");
            MoveTo(OnDialog(details, 0.25), 600);
            Click();
            const string title = "Prepare the launch checklist";
            for (var i = 2; i <= title.Length; i += 2)
            {
                details.Text = title[..Math.Min(i, title.Length)];
                await Refresh();
                Emit((int)(90 * Pace));
            }
            details.Text = title;
            await Refresh();
            Hold(600);

            _caption = "Set a priority and a due date if you like - everything else is optional";
            var priority = (ComboBox)_dialog.FindName("PriorityComboBox");
            MoveTo(OnDialog(priority, 0.4), 700);
            Click();
            priority.SelectedIndex = 0; // High
            await Refresh();
            Hold(600);
            var due = (DatePicker)_dialog.FindName("DueDatePicker");
            MoveTo(OnDialog(due, 0.5), 700);
            Click();
            due.SelectedDate = today;
            await Refresh();
            Hold(1100);

            _caption = "Click Add Task";
            var submit = (Button)_dialog.FindName("SubmitButton");
            MoveTo(OnDialog(submit), 800);
            Click();
            var added = vm.AddCard(title, Column("To Do"), Project("Website Redesign"), "High", today, null, false, null, null);
            details.Text = string.Empty; projectBox.SelectedItem = initialProject; priority.SelectedIndex = 2; due.SelectedDate = null; // back as it opened, so it closes without asking
            _dialog.Close();
            _dialog = null; _dialogShot = null;
            await Refresh();
            _caption = "The new task appears in To Do";
            MoveTo(CentreOnMain(CardBorder(added), 0.85, 0.5), 700);
            Hold(2000);

            // ---- 2. Today's tasks ----
            _caption = "To see only what is due today, click Today";
            Hold(700);
            var todayButton = SidebarButton("Today");
            MoveTo(CentreOnMain(todayButton), 900);
            Click();
            todayButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Refresh();
            Hold(2600);

            _caption = "Clear Filters brings everything back";
            var clear = Walk(_main).OfType<Button>().First(b => (b.Content as string) == "Clear Filters" || Walk(b).OfType<TextBlock>().Any(t => t.Text == "Clear Filters"));
            MoveTo(CentreOnMain(clear), 900);
            Click();
            clear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Refresh();
            Hold(1500);

            // ---- 3. Move tasks ----
            _caption = "To move a task, drag it to another column";
            await Drag(vm, added, "In Progress");
            _caption = "When it is finished, drag it to Done";
            await Drag(vm, added, "Done");

            // ---- Close ----
            _caption = "";
            Hold(500);
            TitleCard("Kanban Task Board", "Download it at hillierconsulting.ca", 3000);

            File.WriteAllLines(Path.Combine(_framesDir, "manifest.txt"), Manifest);
            log.Add($"frames: {Manifest.Count}; length: {Manifest.Sum(m => int.Parse(m.Split('|')[1])) / 1000.0:0.0} s");
            log.Add($"dir: {_framesDir}");
            _main.Close();
        }
        catch (Exception ex) { log.Add("EXCEPTION: " + ex); }
        File.WriteAllLines(Path.Combine(dir, "result.txt"), log);
        Application.Current.Shutdown();
    }
}
