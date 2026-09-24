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
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        _viewModel = viewModel;

        foreach (var column in viewModel.Columns)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = column.Name,
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
            ColumnNamesPanel.Children.Add(row);
        }

        ButtonsOnRightCheckBox.IsChecked = viewModel.IsButtonsOnRight;
        ColumnWidthTextBox.Text = viewModel.ColumnWidth.ToString();
        FitColumnsCheckBox.IsChecked = viewModel.IsFitColumnsToWindow;
        RefreshColumnWidthBox();
        DbPathTextBox.Text = viewModel.CurrentDbPath;
        RefreshRecentFilesList();

        AutoBackupCheckBox.IsChecked = viewModel.AutoBackupEnabled;
        BackupRetentionTextBox.Text = viewModel.BackupRetentionCount.ToString();
        BackupsPathTextBox.Text = BackupService.GetBackupsDir(viewModel.CurrentDbPath);

        ShowSplashCheckBox.IsChecked = viewModel.ShowSplash;
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

        StartFullScreenCheckBox.IsChecked = viewModel.StartFullScreen;
        CompactButtonsCheckBox.IsChecked = viewModel.IsCompactButtons;
        ConfirmDeleteCheckBox.IsChecked = viewModel.ConfirmDelete;
        ConfirmArchiveCheckBox.IsChecked = viewModel.ConfirmArchive;
        AddNoteOnCompleteCheckBox.IsChecked = viewModel.AddNoteOnComplete;
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
    private readonly MainViewModel.SettingsSnapshot _openingSettings;
    private bool _saved;
    private bool _restarting; // the app is shutting down to reopen on another file: nothing to ask

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Text boxes save when they lose focus, which pressing Enter doesn't cause on its own.
        FocusManager.SetFocusedElement(this, null);
        _saved = true;
        Close();
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
        var dialog = new WhatsNewWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();

        // The dialog has its own "show after every update" checkbox, so mirror any change back.
        ShowWhatsNewCheckBox.IsChecked = _viewModel.ShowWhatsNew;
    }

    private void ColumnDisplayNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { Tag: ColumnViewModel column } textBox) return;

        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            textBox.Text = column.DisplayName;
            return;
        }

        _viewModel.RenameColumnDisplayName(column, textBox.Text);
        textBox.Text = column.DisplayName;
    }

    private void CompactButtonsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCompactButtons(CompactButtonsCheckBox.IsChecked == true);
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

    private void FitColumnsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return; // setting IsChecked in the constructor isn't a change
        _viewModel.SetFitColumnsToWindow(FitColumnsCheckBox.IsChecked == true);
        RefreshColumnWidthBox();
    }

    // The pixel width only applies when the columns aren't fitted to the window.
    private void RefreshColumnWidthBox()
    {
        var fixedWidth = FitColumnsCheckBox.IsChecked != true;
        ColumnWidthTextBox.IsEnabled = fixedWidth;
        ColumnWidthLabel.Opacity = fixedWidth ? 1 : 0.5;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow(_viewModel, "HelpSection_Settings") { Owner = this }.ShowDialog();
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
