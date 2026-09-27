using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using KanbanApp.Views;

namespace KanbanApp;

// Quick Add's system-wide hotkey. Windows delivers a registered hotkey to this window as a message
// whichever program is in front, so it only works while the app is running (minimised is fine).
// The Test build uses a different key so it and the installed app can run side by side without
// fighting over one.
public partial class MainWindow
{
    private const int QuickAddHotkeyId = 0x4B41; // arbitrary, unique within this window
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    private const uint VkN = 0x4E;

    public static string QuickAddHotkeyText => AppChannel.IsTest ? "Ctrl+Alt+Shift+N" : "Ctrl+Alt+N";

    private HwndSource? _hotkeySource;
    private bool _hotkeyRegistered;
    private QuickAddWindow? _quickAddWindow;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hotkeySource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hotkeySource?.AddHook(HotkeyHook);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += QuickAddSetting_Changed;
            ApplyQuickAddHotkey(viewModel);
        }

        Closed += (_, _) =>
        {
            UnregisterQuickAddHotkey();
            _hotkeySource?.RemoveHook(HotkeyHook);
            _quickAddWindow?.Close();
        };
    }

    private void QuickAddSetting_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.QuickAddHotkeyEnabled) && sender is MainViewModel viewModel) ApplyQuickAddHotkey(viewModel);
    }

    private void ApplyQuickAddHotkey(MainViewModel viewModel)
    {
        UnregisterQuickAddHotkey();
        viewModel.QuickAddHotkeyProblem = string.Empty;
        if (!viewModel.QuickAddHotkeyEnabled || _hotkeySource is null) return;

        var modifiers = ModControl | ModAlt | ModNoRepeat | (AppChannel.IsTest ? ModShift : 0);
        _hotkeyRegistered = NativeHotkey.RegisterHotKey(_hotkeySource.Handle, QuickAddHotkeyId, modifiers, VkN);
        if (!_hotkeyRegistered)
        {
            viewModel.QuickAddHotkeyProblem = $"{QuickAddHotkeyText} is already in use by another program, so Quick Add can't use it.";
        }
    }

    private void UnregisterQuickAddHotkey()
    {
        if (!_hotkeyRegistered || _hotkeySource is null) return;
        NativeHotkey.UnregisterHotKey(_hotkeySource.Handle, QuickAddHotkeyId);
        _hotkeyRegistered = false;
    }

    private IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == QuickAddHotkeyId && _hotkeyRegistered)
        {
            handled = true;
            // Off the message hook before opening a window.
            Dispatcher.BeginInvoke(new Action(ShowQuickAdd), DispatcherPriority.Normal);
        }

        return IntPtr.Zero;
    }

    // One Quick Add at a time: pressing the hotkey again just brings the open one back to the front.
    private void ShowQuickAdd()
    {
        if (DataContext is not MainViewModel viewModel) return;

        if (_quickAddWindow is not null)
        {
            _quickAddWindow.FocusTaskBox();
            return;
        }

        _quickAddWindow = new QuickAddWindow(viewModel);
        _quickAddWindow.TaskAdded += card => ShowStatusMessage($"Added: {card.Title}");
        _quickAddWindow.Closed += (_, _) => _quickAddWindow = null;
        _quickAddWindow.Show();
        _quickAddWindow.FocusTaskBox();
    }

    private static class NativeHotkey
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
