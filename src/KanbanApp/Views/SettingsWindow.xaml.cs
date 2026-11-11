using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.S || Keyboard.Modifiers != ModifierKeys.Control) return;
            Save_Click(this, new RoutedEventArgs()); // Ctrl+S saves, as everywhere in Windows
            e.Handled = true;
        };
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;
        RestoreSize();
        Closing += (_, _) => SaveSize();

        BuildColumnRows();

        RefreshPrioritiesSummary();
        ButtonsOnRightCheckBox.IsChecked = viewModel.IsButtonsOnRight;
        ColumnWidthTextBox.Text = viewModel.ColumnWidth.ToString();
        MinFittedWidthTextBox.Text = viewModel.MinFittedColumnWidth.ToString();
        ColumnWidthHint.Text = $"From {MainViewModel.SmallestColumnWidth} to {MainViewModel.MaxColumnWidth}. Below about 210, the buttons on each task go onto two rows.";
        FitColumnsCheckBox.IsChecked = viewModel.IsFitColumnsToWindow;
        RefreshColumnWidthBox();
        DbPathTextBox.Text = viewModel.CurrentDbPath;

        AutoBackupCheckBox.IsChecked = viewModel.AutoBackupEnabled;
        BackupRetentionTextBox.Text = viewModel.BackupRetentionCount.ToString();
        BackupsPathTextBox.Text = BackupService.GetBackupsDir(viewModel.CurrentDbPath);

        ShowSplashCheckBox.IsChecked = viewModel.ShowSplash;
        WeekStartComboBox.SelectedIndex = viewModel.WeekStartsOnSunday ? 1 : 0;
        TaskViewComboBox.SelectedIndex = viewModel.SimplifiedTaskWindow ? 1 : 0;
        foreach (System.Windows.Controls.ComboBoxItem item in SplashDelayComboBox.Items)
        {
            if (item.Tag is string tag && int.TryParse(tag, out var ms) && ms == viewModel.SplashDelayMs)
            {
                SplashDelayComboBox.SelectedItem = item;
                break;
            }
        }
        SplashDelayComboBox.SelectedItem ??= SplashDelayComboBox.Items[1];

        ExportPathTextBox.Text = viewModel.DefaultExportPath;
        ImportPathTextBox.Text = viewModel.DefaultImportPath;
        LinkedFilesPathTextBox.Text = viewModel.LinkedFilesDefaultPath;

        UserNameTextBox.Text = viewModel.UserName;
        UserTitleTextBox.Text = viewModel.UserTitle;
        UserEmailTextBox.Text = viewModel.UserEmail;
        UserPhoneTextBox.Text = viewModel.UserPhone;

        StartWithWindowsCheckBox.IsChecked = viewModel.StartWithWindows;
        StartFullScreenCheckBox.IsChecked = viewModel.StartFullScreen;
        CompactButtonsCheckBox.IsChecked = viewModel.IsCompactButtons;
        BuildCardPartBoxes(viewModel);
        AutoCorrectCheckBox.IsChecked = viewModel.AutoCorrectEnabled;
        AutoCorrectEntriesTextBox.Text = viewModel.AutoCorrectEntries;
        AutoCorrectEntriesTextBox.IsEnabled = viewModel.AutoCorrectEnabled;
        ConfirmDeleteCheckBox.IsChecked = viewModel.ConfirmDelete;
        ConfirmArchiveCheckBox.IsChecked = viewModel.ConfirmArchive;
        AddNoteOnCompleteCheckBox.IsChecked = viewModel.AddNoteOnComplete;
        NotifySenderCheckBox.IsChecked = viewModel.NotifySenderOnDone;
        CustomDictionaryTextBox.Text = viewModel.CustomDictionaryText;
        ShowDueRemindersCheckBox.IsChecked = viewModel.ShowDueReminders;
        ShowTimeAlertsCheckBox.IsChecked = viewModel.ShowTimeAlerts;
        QuickAddHotkeyCheckBox.Content = $"Quick Add: {MainWindow.QuickAddHotkeyText} opens a New Task box from any program";
        QuickAddHotkeyCheckBox.IsChecked = viewModel.QuickAddHotkeyEnabled;
        CheckForUpdatesCheckBox.IsChecked = viewModel.CheckForUpdatesEnabled;
        ShowQuickAddHotkeyProblem();
        RememberLastViewCheckBox.IsChecked = viewModel.RememberLastView;
        ShowWhatsNewCheckBox.IsChecked = viewModel.ShowWhatsNew;

        _openingSettings = viewModel.CaptureSettings();
    }

    // Every change on this dialog is saved as it's made, so Cancel (and Esc, and the window's X)
    // works by putting back the snapshot taken when the dialog opened - see
    // MainViewModel.RestoreSettings for what that covers. Asks first if anything actually changed,
    // the same as the task dialog does.
    private MainViewModel.SettingsSnapshot _openingSettings;
    private bool _saved;
    private bool _restarting; // the app is shutting down to reopen on another file: nothing to ask

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Text boxes save when they lose focus, which pressing Enter doesn't cause on its own.
        FocusManager.SetFocusedElement(this, null);
        _saved = true;
        Close();
    }

    // Until it is resized the window fits its content; once resized, it opens at that size again
    // (never bigger than the screen - the size may have come from a larger monitor).
    private void RestoreSize()
    {
        if (_viewModel.WindowSize("Settings") is not { } size) return;

        var area = SystemParameters.WorkArea;
        SizeToContent = SizeToContent.Manual;
        Width = Math.Clamp(size.Width, MinWidth, Math.Max(MinWidth, area.Width));
        Height = Math.Clamp(size.Height, MinHeight, Math.Max(MinHeight, area.Height));
        MaxHeight = Math.Max(MaxHeight, Height);
        if (size.Maximized) WindowState = WindowState.Maximized;
    }

    // Only a size the user chose: resizing turns SizeToContent off. A window never resized keeps
    // fitting its content, so a setting added later still shows without scrolling.
    private void SaveSize()
    {
        if (SizeToContent != SizeToContent.Manual && WindowState == WindowState.Normal) return;

        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        _viewModel.SaveWindowSize("Settings", bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _saved || _restarting) return;

        // A text box being edited hasn't saved yet; commit it so it's part of what gets compared
        // and put back.
        FocusManager.SetFocusedElement(this, null);
        Keyboard.ClearFocus();
        if (_viewModel.CaptureSettings() == _openingSettings) return;

        if (!UnsavedChangesGuard.ConfirmDiscard(this))
        {
            e.Cancel = true;
            return;
        }

        _viewModel.RestoreSettings(_openingSettings);
    }

    private void ShowWhatsNewCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowWhatsNew(ShowWhatsNewCheckBox.IsChecked == true);
    }

    private void ShowWhatsNew_Click(object sender, RoutedEventArgs e)
    {
        HelpWindow.Open(_viewModel, showWhatsNew: true);
    }

    // A ready-made set of names for all five columns. Like typing them in, it is undone by Cancel.
    private void UseGtdNames_Click(object sender, RoutedEventArgs e) => UseColumnNames(MainViewModel.GtdColumnNames);

    private void UseStandardNames_Click(object sender, RoutedEventArgs e) => UseColumnNames(MainViewModel.StandardColumnNames);

    private void UseColumnNames(IReadOnlyList<string> names)
    {
        _viewModel.UseColumnNames(names);
        foreach (var box in ColumnNamesPanel.Children.OfType<Panel>().SelectMany(row => row.Children.OfType<TextBox>()))
        {
            if (box.Tag is ColumnViewModel column) box.Text = column.DisplayName;
        }
    }

    // The priority list has its own screen, where each change is made straight away, as on the other
    // Manage screens. It is not a Settings preference, so Cancel here does not take those changes back.
    private void ManagePriorities_Click(object sender, RoutedEventArgs e)
    {
        new ManagePrioritiesWindow(_viewModel) { Owner = this }.ShowDialog();
        RefreshPrioritiesSummary();
    }

    private void RefreshPrioritiesSummary() =>
        PrioritiesSummaryText.Text = $"Highest first: {string.Join(", ", _viewModel.Priorities.Names)}. New tasks start at {_viewModel.Priorities.Default}.";

    // One row per column: its own name, a box for its label and, for a column that was added, buttons to move or
    // remove it.
    private void BuildColumnRows()
    {
        ColumnNamesPanel.Children.Clear();
        foreach (var column in _viewModel.Columns)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = column.IsCustom ? "Added" : column.Name,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush")
            });
            var textBox = new TextBox
            {
                Text = column.DisplayName,
                Width = 220,
                Padding = new Thickness(6),
                Tag = column,
                Background = (System.Windows.Media.Brush)FindResource("InputBackgroundBrush"),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("CardBorderBrush")
            };
            textBox.LostFocus += ColumnDisplayNameTextBox_LostFocus;
            row.Children.Add(textBox);

            if (column.IsCustom)
            {
                row.Children.Add(ColumnButton("\u25B2", "Move this column left", _viewModel.CanMoveColumn(column, -1), () => _viewModel.MoveColumn(column, -1), 6));
                row.Children.Add(ColumnButton("\u25BC", "Move this column right", _viewModel.CanMoveColumn(column, 1), () => _viewModel.MoveColumn(column, 1), 4));
                row.Children.Add(ColumnButton("\u2715", "Remove this column", true, () => RemoveColumn(column), 4));
            }
            ColumnNamesPanel.Children.Add(row);
        }

        var room = MainViewModel.MaxColumns - _viewModel.Columns.Count;
        NewColumnTextBox.IsEnabled = AddColumnButton.IsEnabled = room > 0;
        ColumnLimitHint.Text = room > 0
            ? $"You can add {room} more column{(room == 1 ? "" : "s")}, {MainViewModel.MaxColumns} in all."
            : $"The board has {MainViewModel.MaxColumns} columns, the most it can have. Remove one to add another.";
    }

    private Button ColumnButton(string symbol, string toolTip, bool enabled, Action action, double leftMargin)
    {
        var button = new Button
        {
            Content = symbol,
            ToolTip = toolTip,
            IsEnabled = enabled,
            Width = 28,
            Margin = new Thickness(leftMargin, 0, 0, 0),
            Background = (System.Windows.Media.Brush)FindResource("ButtonBackgroundBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
        };
        button.Click += (_, _) => { action(); AfterColumnsChanged(); };
        return button;
    }

    // Adding, moving and removing a column happen at once and Cancel does not undo them, so what Cancel compares
    // with and puts back is brought up to date with the columns there now.
    private void AfterColumnsChanged()
    {
        _openingSettings = _viewModel.WithCurrentColumns(_openingSettings);
        BuildColumnRows();
    }

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.AddColumn(NewColumnTextBox.Text) is { } problem)
        {
            Dialogs.Tell(this, "Add a Column", problem);
            return;
        }

        NewColumnTextBox.Clear();
        AfterColumnsChanged();
    }

    private void NewColumnTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddColumn_Click(sender, e);
        e.Handled = true;
    }

    private void RemoveColumn(ColumnViewModel column)
    {
        var count = _viewModel.TasksInColumn(column);
        var text = $"Remove the column \"{column.DisplayName}\"?";
        if (count > 0) text += $"\n\n{count} task{(count == 1 ? " is" : "s are")} in it (archived and deleted ones too). {(count == 1 ? "It" : "They")} will move to {_viewModel.ToDoColumn?.DisplayName ?? "the first column"}.";
        text += "\n\nThis can't be undone, and Cancel in Settings won't bring it back.";
        if (!Dialogs.Confirm(this, DialogMessage.AskDanger("Remove Column", text, "Remove"))) return;

        _viewModel.RemoveColumn(column);
    }

    private void ColumnDisplayNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { Tag: ColumnViewModel column } textBox) return;

        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            textBox.Text = column.DisplayName;
            return;
        }

        // Two columns with one label are hard to tell apart (and a file names a column added by its label), so a
        // column that was added can't share one. The standard five can be swapped round freely, as before.
        var typed = textBox.Text.Trim();
        if (_viewModel.Columns.FirstOrDefault(c => c != column && (c.IsCustom || column.IsCustom) && string.Equals(c.DisplayName, typed, StringComparison.CurrentCultureIgnoreCase)) is { } clash)
        {
            Dialogs.Tell(this, "Rename Column", $"There is already a column called \"{clash.DisplayName}\".");
            textBox.Text = column.DisplayName;
            return;
        }

        _viewModel.RenameColumnDisplayName(column, textBox.Text);
        textBox.Text = column.DisplayName;
    }

    private void AutoCorrectCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _viewModel.SetAutoCorrectEnabled(AutoCorrectCheckBox.IsChecked == true);
        AutoCorrectEntriesTextBox.IsEnabled = AutoCorrectCheckBox.IsChecked == true;
    }

    // Saved as it is typed, like every other control here: waiting for the box to lose focus lost an entry
    // typed just before Ctrl+S or closing the window.
    private void AutoCorrectEntriesTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded) _viewModel.SetAutoCorrectEntries(AutoCorrectEntriesTextBox.Text);
    }

    // One tick box per part a large card can show, two to a row. Each change is saved at once, like the
    // rest of this window, and Cancel puts the lot back through the snapshot.
    private void BuildCardPartBoxes(MainViewModel viewModel)
    {
        foreach (var part in Enum.GetValues<CardPart>())
        {
            var box = new System.Windows.Controls.CheckBox
            {
                Content = CardPartsShown.Label(part), IsChecked = viewModel.CardParts.Shows(part), Margin = new Thickness(0, 0, 8, 8)
            };
            box.SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            box.Checked += (_, _) => viewModel.SetCardPartShown(part, true);
            box.Unchecked += (_, _) => viewModel.SetCardPartShown(part, false);
            CardPartsGrid.Children.Add(box);
        }
    }

    private void CompactButtonsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCompactButtons(CompactButtonsCheckBox.IsChecked == true);
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetStartWithWindows(StartWithWindowsCheckBox.IsChecked == true);
    }

    private void StartFullScreenCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetStartFullScreen(StartFullScreenCheckBox.IsChecked == true);
    }

    private void ConfirmDeleteCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetConfirmDelete(ConfirmDeleteCheckBox.IsChecked == true);
    }

    private void ConfirmArchiveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetConfirmArchive(ConfirmArchiveCheckBox.IsChecked == true);
    }

    // Saved as it is typed, like every other control here, so Ctrl+S or closing straight after loses nothing.
    private void CustomDictionaryTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded) _viewModel.SetCustomDictionary(CustomDictionaryTextBox.Text);
    }

    private void NotifySenderCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _viewModel.SetNotifySenderOnDone(NotifySenderCheckBox.IsChecked == true);
    }

    private void AddNoteOnCompleteCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAddNoteOnComplete(AddNoteOnCompleteCheckBox.IsChecked == true);
    }

    private void ShowDueRemindersCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowDueReminders(ShowDueRemindersCheckBox.IsChecked == true);
    }

    private void CheckForUpdatesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCheckForUpdatesEnabled(CheckForUpdatesCheckBox.IsChecked == true);
    }

    private void QuickAddHotkeyCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetQuickAddHotkeyEnabled(QuickAddHotkeyCheckBox.IsChecked == true);
        ShowQuickAddHotkeyProblem();
    }

    // The main window re-registers the key as soon as the setting changes, so by the time this
    // reads it the view-model already knows whether Windows handed the key over.
    private void ShowQuickAddHotkeyProblem()
    {
        QuickAddHotkeyProblemText.Text = _viewModel.QuickAddHotkeyProblem;
        QuickAddHotkeyProblemText.Visibility = string.IsNullOrEmpty(_viewModel.QuickAddHotkeyProblem) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowTimeAlertsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowTimeAlerts(ShowTimeAlertsCheckBox.IsChecked == true);
    }

    private void RememberLastViewCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRememberLastView(RememberLastViewCheckBox.IsChecked == true);
    }

    private void ButtonsOnRightCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var wantRight = ButtonsOnRightCheckBox.IsChecked == true;
        if (wantRight != _viewModel.IsButtonsOnRight)
        {
            _viewModel.ToggleButtonPosition();
        }
    }

    private void ColumnWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ColumnWidthTextBox.Text.Trim(), out var width))
        {
            ColumnWidthTextBox.Text = _viewModel.ColumnWidth.ToString();
            return;
        }

        _viewModel.SetColumnWidth(width);
        ColumnWidthTextBox.Text = _viewModel.ColumnWidth.ToString();
    }

    private void MinFittedWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(MinFittedWidthTextBox.Text.Trim(), out var width)) _viewModel.SetMinFittedColumnWidth(width);
        MinFittedWidthTextBox.Text = _viewModel.MinFittedColumnWidth.ToString();
    }

    private void FitColumnsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return; // setting IsChecked in the constructor isn't a change
        _viewModel.SetFitColumnsToWindow(FitColumnsCheckBox.IsChecked == true);
        RefreshColumnWidthBox();
    }

    // Fitted, the narrowest width applies; not fitted, the fixed width does. The other is greyed out.
    private void RefreshColumnWidthBox()
    {
        var fixedWidth = FitColumnsCheckBox.IsChecked != true;
        ColumnWidthTextBox.IsEnabled = fixedWidth;
        ColumnWidthLabel.Opacity = fixedWidth ? 1 : 0.5;
        MinFittedWidthTextBox.IsEnabled = !fixedWidth;
        MinFittedWidthLabel.Opacity = fixedWidth ? 0.5 : 1;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        HelpWindow.Open(_viewModel, "HelpSection_Settings");
    }

    private void TaskViewComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TaskViewComboBox.SelectedIndex >= 0) _viewModel.SetSimplifiedTaskWindow(TaskViewComboBox.SelectedIndex == 1);
    }

    private void WeekStartComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (WeekStartComboBox.SelectedIndex >= 0) _viewModel.SetWeekStartsOnSunday(WeekStartComboBox.SelectedIndex == 1);
    }

    private void ShowSplashCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowSplash(ShowSplashCheckBox.IsChecked == true);
    }

    private void SplashDelayComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SplashDelayComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string tag } &&
            int.TryParse(tag, out var ms))
        {
            _viewModel.SetSplashDelayMs(ms);
        }
    }
}
