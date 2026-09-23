using System.Windows.Controls;
using System.Windows.Media;

namespace KanbanApp.ViewModels;

// App-level settings: theme, layout, splash, default paths, confirmation toggles, and the
// remember-last-view persistence. See MainViewModel.cs for the shared state this reads/writes.
public partial class MainViewModel
{
    private bool _isDarkMode;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set
        {
            if (SetField(ref _isDarkMode, value))
            {
                OnPropertyChanged(nameof(ThemeButtonLabel));
            }
        }
    }

    public string ThemeButtonLabel => IsDarkMode ? "Light Mode" : "Dark Mode";

    private bool _isButtonsOnRight;
    public bool IsButtonsOnRight
    {
        get => _isButtonsOnRight;
        set
        {
            if (SetField(ref _isButtonsOnRight, value))
            {
                OnPropertyChanged(nameof(SidebarDock));
                OnPropertyChanged(nameof(SidebarCollapseGlyph));
                OnPropertyChanged(nameof(SidebarExpandGlyph));
                OnPropertyChanged(nameof(SidebarHideAlignment));
            }
        }
    }

    public Dock SidebarDock => IsButtonsOnRight ? Dock.Right : Dock.Left;

    private bool _isCompactCards;
    public bool IsCompactCards
    {
        get => _isCompactCards;
        set
        {
            if (SetField(ref _isCompactCards, value))
            {
                OnPropertyChanged(nameof(IsLargeCards));
                OnPropertyChanged(nameof(CardSizeButtonLabel));
            }
        }
    }

    public bool IsLargeCards => !IsCompactCards;
    public string CardSizeButtonLabel => IsCompactCards ? "Large Cards" : "Compact Cards";

    // "Hide Future": keeps tasks off the board until their start date arrives. A view setting like
    // card size - always remembered, and not touched by Clear Filters or custom filters.
    private bool _hideFutureTasks;
    public bool HideFutureTasks
    {
        get => _hideFutureTasks;
        private set
        {
            if (SetField(ref _hideFutureTasks, value)) OnPropertyChanged(nameof(HideFutureButtonLabel));
        }
    }

    // While hiding, the button says how many, so hidden tasks are never forgotten about.
    public string HideFutureButtonLabel => HideFutureTasks ? $"Show Future ({FutureTaskCount})" : "Hide Future";

    // How many tasks the setting is hiding (or would hide) right now - shown on the button's tooltip.
    public int FutureTaskCount => Columns.SelectMany(c => c.Cards).Count(c => c.IsNotStarted && c.CompletedAt is null);

    public void ToggleHideFutureTasks()
    {
        HideFutureTasks = !HideFutureTasks;
        _db.SetSetting("HideFutureTasks", HideFutureTasks ? "True" : "False");
        ApplyFilters();
    }

    // Called on a timer: when the date rolls over while the app is open, "today" has changed for
    // overdue highlighting, the Starts line and Hide Future, so bring them up to date.
    private DateTime _lastSeenDay = DateTime.Today;

    public void RefreshIfDayChanged()
    {
        if (_lastSeenDay == DateTime.Today) return;

        _lastSeenDay = DateTime.Today;
        RefreshDashboardStats();
        ApplyFilters();
    }

    private int _columnWidth = 310;
    public int ColumnWidth
    {
        get => _columnWidth;
        set
        {
            if (SetField(ref _columnWidth, value)) OnPropertyChanged(nameof(EffectiveColumnWidth));
        }
    }

    public void SetColumnWidth(int value)
    {
        ColumnWidth = Math.Clamp(value, MinColumnWidth, 800);
        _db.SetSetting("ColumnWidth", ColumnWidth.ToString());
    }

    // Fit to window: the task columns share the board's width evenly and follow it as the window
    // is resized or the button column is shown or hidden. Off, every column is ColumnWidth wide.
    // Either way a board too narrow for its columns scrolls sideways.
    public const double ColumnGap = 12;             // each column's right margin on the board
    // The narrowest a task column can be, fitted or not: room for a card's six quick buttons (147px)
    // plus the column and card padding and the list's scrollbar (59px), with some to spare. Below it,
    // fitting gives way to scrolling sideways.
    public const int MinColumnWidth = 240;

    private bool _isFitColumnsToWindow = true;
    public bool IsFitColumnsToWindow
    {
        get => _isFitColumnsToWindow;
        private set
        {
            if (SetField(ref _isFitColumnsToWindow, value)) OnPropertyChanged(nameof(EffectiveColumnWidth));
        }
    }

    public void SetFitColumnsToWindow(bool value)
    {
        IsFitColumnsToWindow = value;
        _db.SetSetting("FitColumnsToWindow", value ? "True" : "False");
    }

    private double _boardWidth;

    // Told by the board whenever its width changes.
    public void SetBoardWidth(double width)
    {
        if (!double.IsFinite(width) || Math.Abs(width - _boardWidth) < 0.5) return;
        _boardWidth = width;
        OnPropertyChanged(nameof(EffectiveColumnWidth));
    }

    // What the board actually draws each column at. A pixel is kept back so rounding on a scaled
    // display can't tip the row over the edge and bring up a scrollbar for nothing.
    public double EffectiveColumnWidth => FittedColumnWidth(IsFitColumnsToWindow, _boardWidth, Columns.Count, ColumnWidth);

    internal static double FittedColumnWidth(bool fit, double boardWidth, int columnCount, int fixedWidth)
    {
        if (!fit || boardWidth <= 0 || columnCount == 0) return fixedWidth;
        return Math.Max(MinColumnWidth, Math.Floor((boardWidth - 1) / columnCount - ColumnGap));
    }

    // Heights of the sidebar's multi-select filter lists, adjusted by dragging the grip under each.
    // Priority and Who share a row, so they share one height.
    public const double DefaultFilterListHeight = 140;
    public const double MinFilterListHeight = 50;
    public const double MaxFilterListHeight = 600;

    private double _projectFilterListHeight = DefaultFilterListHeight;
    public double ProjectFilterListHeight
    {
        get => _projectFilterListHeight;
        set => SetField(ref _projectFilterListHeight, ClampFilterListHeight(value));
    }

    private double _priorityWhoFilterListHeight = DefaultFilterListHeight;
    public double PriorityWhoFilterListHeight
    {
        get => _priorityWhoFilterListHeight;
        set => SetField(ref _priorityWhoFilterListHeight, ClampFilterListHeight(value));
    }

    // No rounding here: drags arrive in fractional steps on scaled displays, and rounding each one
    // away would leave the grip stuck.
    internal static double ClampFilterListHeight(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, MinFilterListHeight, MaxFilterListHeight) : DefaultFilterListHeight;

    // Called when a drag ends (not on every mouse move), so the database isn't written per pixel.
    public void SaveFilterListHeights()
    {
        _db.SetSetting("ProjectFilterListHeight", ProjectFilterListHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _db.SetSetting("PriorityWhoFilterListHeight", PriorityWhoFilterListHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static double LoadFilterListHeight(string? stored) =>
        double.TryParse(stored, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height)
            ? ClampFilterListHeight(height)
            : DefaultFilterListHeight;

    // The Timeline window's size as it was last left, stored as "width,height,maximized". The
    // position isn't kept: the window always opens centred on the board.
    public (double Width, double Height, bool Maximized)? TimelineWindowSize
    {
        get
        {
            var parts = (_db.GetSetting("TimelineWindowSize") ?? string.Empty).Split(',');
            if (parts.Length != 3) return null;
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, invariant, out var width)
                || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, invariant, out var height)
                || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return null;
            return (width, height, parts[2] == "1");
        }
    }

    public void SaveTimelineWindowSize(double width, double height, bool maximized)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        _db.SetSetting("TimelineWindowSize", $"{width.ToString(invariant)},{height.ToString(invariant)},{(maximized ? "1" : "0")}");
    }

    // Whether the Timeline was last left in Day view (otherwise Week view, which is also the default).
    public bool TimelineDayView => _db.GetSetting("TimelineView") == "Day";

    public void SaveTimelineDayView(bool dayView) => _db.SetSetting("TimelineView", dayView ? "Day" : "Week");

    public bool ShowSplash { get; private set; }
    public int SplashDelayMs { get; private set; }

    public void SetShowSplash(bool value)
    {
        ShowSplash = value;
        _db.SetSetting("ShowSplash", value ? "True" : "False");
    }

    public void SetSplashDelayMs(int value)
    {
        SplashDelayMs = value;
        _db.SetSetting("SplashDelayMs", value.ToString());
    }

    public string DefaultExportPath { get; private set; } = string.Empty;
    public string DefaultImportPath { get; private set; } = string.Empty;
    public string LinkedFilesDefaultPath { get; private set; } = string.Empty;

    public void SetDefaultExportPath(string value)
    {
        DefaultExportPath = value;
        _db.SetSetting("DefaultExportPath", value);
    }

    public void SetDefaultImportPath(string value)
    {
        DefaultImportPath = value;
        _db.SetSetting("DefaultImportPath", value);
    }

    public void SetLinkedFilesDefaultPath(string value)
    {
        LinkedFilesDefaultPath = value;
        _db.SetSetting("LinkedFilesDefaultPath", value);
    }

    // Used to build a fallback email signature (Email This Task) when Outlook has no default
    // signature of its own configured. All optional - an empty one is simply left out of the block.
    public string UserName { get; private set; } = string.Empty;
    public string UserTitle { get; private set; } = string.Empty;
    public string UserEmail { get; private set; } = string.Empty;
    public string UserPhone { get; private set; } = string.Empty;

    public void SetUserName(string value)
    {
        UserName = value.Trim();
        _db.SetSetting("UserName", UserName);
    }

    public void SetUserTitle(string value)
    {
        UserTitle = value.Trim();
        _db.SetSetting("UserTitle", UserTitle);
    }

    public void SetUserEmail(string value)
    {
        UserEmail = value.Trim();
        _db.SetSetting("UserEmail", UserEmail);
    }

    public void SetUserPhone(string value)
    {
        UserPhone = value.Trim();
        _db.SetSetting("UserPhone", UserPhone);
    }

    // A backup copy of the database is written each time the app closes (see BackupService),
    // pruned down to the most recent BackupRetentionCount afterward.
    public bool AutoBackupEnabled { get; private set; } = true;
    public int BackupRetentionCount { get; private set; } = 20;

    public void SetAutoBackupEnabled(bool value)
    {
        AutoBackupEnabled = value;
        _db.SetSetting("AutoBackupEnabled", value ? "True" : "False");
    }

    public void SetBackupRetentionCount(int value)
    {
        BackupRetentionCount = Math.Clamp(value, 1, 200);
        _db.SetSetting("BackupRetentionCount", BackupRetentionCount.ToString());
    }

    public bool StartFullScreen { get; private set; }
    public bool ConfirmDelete { get; private set; } = true;
    public bool ConfirmArchive { get; private set; } = true;
    public bool AddNoteOnComplete { get; private set; }
    public bool ShowDueReminders { get; private set; } = true;
    public bool ShowTimeAlerts { get; private set; } = true;
    public bool RememberLastView { get; private set; }

    public void SetStartFullScreen(bool value)
    {
        StartFullScreen = value;
        _db.SetSetting("StartFullScreen", value ? "True" : "False");
    }

    public void SetConfirmDelete(bool value)
    {
        ConfirmDelete = value;
        _db.SetSetting("ConfirmDelete", value ? "True" : "False");
    }

    public void SetConfirmArchive(bool value)
    {
        ConfirmArchive = value;
        _db.SetSetting("ConfirmArchive", value ? "True" : "False");
    }

    public void SetAddNoteOnComplete(bool value)
    {
        AddNoteOnComplete = value;
        _db.SetSetting("AddNoteOnComplete", value ? "True" : "False");
    }

    public void SetShowDueReminders(bool value)
    {
        ShowDueReminders = value;
        _db.SetSetting("ShowDueReminders", value ? "True" : "False");
    }

    public void SetShowTimeAlerts(bool value)
    {
        ShowTimeAlerts = value;
        _db.SetSetting("ShowTimeAlerts", value ? "True" : "False");
    }

    public void SetRememberLastView(bool value)
    {
        RememberLastView = value;
        _db.SetSetting("RememberLastView", value ? "True" : "False");
    }

    // How many past releases the What's New screen lists.
    public const int WhatsNewVersionCount = 5;

    public bool ShowWhatsNew { get; private set; } = true;

    public void SetShowWhatsNew(bool value)
    {
        ShowWhatsNew = value;
        _db.SetSetting("ShowWhatsNew", value ? "True" : "False");
    }

    // True when this build differs from the one last acknowledged, i.e. the app has just been
    // updated. A brand-new install is deliberately excluded: with no cards on the board there's
    // nothing to have "updated" from, and a changelog is a poor first thing to greet someone with.
    // Some things belong to the PC, not to the task file - and two PCs can share one task file (a
    // synced folder). Those are stored under a key that carries the computer's name: when this PC
    // last checked for an update, and which version's What's New it has shown. Kept in the task
    // file all the same, so tests and self-checks never touch anything outside their own folder.
    internal string ThisComputer { get; set; } = Environment.MachineName;

    private string LastSeenVersionKey => $"LastSeenVersion:{ThisComputer}";

    // An existing user upgrading into this feature has no stored version yet but does have cards,
    // so they still get the screen the first time. A PC with nothing of its own recorded yet falls
    // back to the old shared value, so a single-PC user upgrading isn't shown the screen twice.
    public bool ShouldShowWhatsNewOnStartup()
    {
        if (!ShowWhatsNew) return false;

        var lastSeen = _db.GetSetting(LastSeenVersionKey) ?? _db.GetSetting("LastSeenVersion");
        if (string.IsNullOrEmpty(lastSeen)) return Columns.Any(c => c.Cards.Count > 0);

        return lastSeen != AppVersion;
    }

    public void MarkWhatsNewSeen()
    {
        _db.SetSetting(LastSeenVersionKey, AppVersion);
    }

    public void SaveLastViewState()
    {
        if (!RememberLastView) return;

        _db.SetSetting("LastProjectFilter", string.Join(",", ProjectFilterOptions.Where(o => o.IsSelected).Select(o => o.Name)));
        _db.SetSetting("LastPriorityFilter", string.Join(",", PriorityFilterOptions.Where(o => o.IsSelected).Select(o => o.Name)));
        _db.SetSetting("LastWhoFilter", string.Join(",", WhoFilterOptions.Where(o => o.IsSelected).Select(o => o.Name)));
        _db.SetSetting("LastGoalFilter", SelectedGoalFilter);
        _db.SetSetting("LastFlagFilter", SelectedFlagFilter);
        _db.SetSetting("LastDueFilter", DueFilter);
        _db.SetSetting("LastDueRangeFrom", DueRangeFrom?.ToString("yyyy-MM-dd") ?? string.Empty);
        _db.SetSetting("LastDueRangeTo", DueRangeTo?.ToString("yyyy-MM-dd") ?? string.Empty);
        _db.SetSetting("LastKeywordFilter", KeywordFilter);
        _db.SetSetting("LastSortMode", string.Join(",", _sortKeys));
    }

    private static readonly Brush[] ColumnPaletteLight =
    [
        new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xEF)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xCD)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0xE1, 0xD5, 0xF5)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA)), // Done - green
    ];

    private static readonly Brush[] ColumnPaletteDark =
    [
        new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x38)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0x4D, 0x42, 0x20)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0x4D, 0x34, 0x19)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0x3B, 0x2F, 0x4D)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0x20, 0x40, 0x30)), // Done - green
    ];

    public void ToggleButtonPosition()
    {
        IsButtonsOnRight = !IsButtonsOnRight;
        _db.SetSetting("ButtonPosition", IsButtonsOnRight ? "Right" : "Left");
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        _db.SetSetting("Theme", IsDarkMode ? "Dark" : "Light");
        Theming.ThemeManager.Apply(IsDarkMode);
        ApplyColumnPalette();
    }

    private void ApplyColumnPalette()
    {
        var palette = IsDarkMode ? ColumnPaletteDark : ColumnPaletteLight;
        for (var i = 0; i < Columns.Count; i++)
        {
            Columns[i].Background = palette[i % palette.Length];
        }
    }

    public void ToggleCardSize()
    {
        IsCompactCards = !IsCompactCards;
        _db.SetSetting("CardSize", IsCompactCards ? "Compact" : "Large");
    }
}
