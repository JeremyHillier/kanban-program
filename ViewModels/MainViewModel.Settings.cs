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

    private int _columnWidth = 310;
    public int ColumnWidth
    {
        get => _columnWidth;
        set => SetField(ref _columnWidth, value);
    }

    public void SetColumnWidth(int value)
    {
        ColumnWidth = Math.Clamp(value, 150, 800);
        _db.SetSetting("ColumnWidth", ColumnWidth.ToString());
    }

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

    public bool StartFullScreen { get; private set; }
    public bool ConfirmDelete { get; private set; } = true;
    public bool ConfirmArchive { get; private set; } = true;
    public bool AddNoteOnComplete { get; private set; }
    public bool ShowDueReminders { get; private set; } = true;
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
    // An existing user upgrading into this feature has no stored version yet but does have cards,
    // so they still get the screen the first time.
    public bool ShouldShowWhatsNewOnStartup()
    {
        if (!ShowWhatsNew) return false;

        var lastSeen = _db.GetSetting("LastSeenVersion");
        if (string.IsNullOrEmpty(lastSeen)) return Columns.Any(c => c.Cards.Count > 0);

        return lastSeen != AppVersion;
    }

    public void MarkWhatsNewSeen()
    {
        _db.SetSetting("LastSeenVersion", AppVersion);
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
