using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Forms;
using PasteByDan.Models;
using PasteByDan.Services;
using PasteByDan.ViewModels;

namespace PasteByDan
{
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;
        private IntPtr _hwnd;
        private IntPtr _prevHwnd;
        private NotifyIcon _trayIcon;
        private bool _settingsOpen = false;
        private bool _isDark = true;
        private IntPtr _winEventHook;
        private Win32.WinEventDelegate _winEventDelegate;

        private const int HOTKEY_ID = 9001;
        private const uint MOD_CTRL_SHIFT = Win32.MOD_CONTROL | Win32.MOD_SHIFT;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            _vm.CopyCommand = new RelayCommand<ClipboardItem>(CopyItem);
            _vm.PasteCommand = new RelayCommand<ClipboardItem>(PasteItem);
            DataContext = _vm;

            PositionWindow();
            SetupTray();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            _hwnd = source.Handle;
            source.AddHook(WndProc);

            Win32.AddClipboardFormatListener(_hwnd);
            RegisterHotkey();

            // Track last real foreground window continuously
            _winEventDelegate = new Win32.WinEventDelegate(OnForegroundChanged);
            _winEventHook = Win32.SetWinEventHook(
                Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        }

        private void RegisterHotkey()
        {
            Win32.UnregisterHotKey(_hwnd, HOTKEY_ID);
            Win32.RegisterHotKey(_hwnd, HOTKEY_ID, Win32.MOD_CONTROL | Win32.MOD_SHIFT, 0x56);
        }

        protected override void OnClosed(EventArgs e)
        {
            Win32.RemoveClipboardFormatListener(_hwnd);
            Win32.UnregisterHotKey(_hwnd, HOTKEY_ID);
            if (_winEventHook != IntPtr.Zero) Win32.UnhookWinEvent(_winEventHook);
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)Win32.WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
                handled = true;
            }
            if (msg == (int)Win32.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindow();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            if (ClipboardService.ConsumeIgnoreNext()) return;
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var item = ClipboardService.GetCurrentClipboardItem();
                    if (item != null) _vm.AddItem(item);
                }
                catch { }
            });
        }

        private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Keep track of the last real foreground window (not ours)
            if (hwnd != IntPtr.Zero && hwnd != _hwnd)
            {
                _prevHwnd = hwnd;
                Log($"FG changed: {hwnd.ToInt64():X}");
            }
        }

        private void ToggleWindow()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Log($"ToggleWindow show: prevHwnd={_prevHwnd.ToInt64():X}");
                PositionWindow();
                Show();
                Activate();
                SearchBox.Focus();
            }
        }

        private void PositionWindow()
        {
            var wa = SystemParameters.WorkArea;
            Width = wa.Width;
            Height = wa.Height * 0.27;
            Left = wa.Left;
            Top = wa.Bottom - Height;
        }

        private void SetupTray()
        {
            _trayIcon = new NotifyIcon();
            try
            {
                _trayIcon.Icon = new System.Drawing.Icon(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "tray.ico"));
            }
            catch
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            _trayIcon.Text = "Paste by Dan";
            _trayIcon.Visible = true;

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) Dispatcher.Invoke(ToggleWindow);
            };

            var cm = new ContextMenuStrip();
            cm.Items.Add("Show / Hide", null, (s, e) => Dispatcher.Invoke(ToggleWindow));
            cm.Items.Add("-");
            cm.Items.Add("Quit", null, (s, e) =>
            {
                _trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });
            _trayIcon.ContextMenuStrip = cm;
        }

        // ----- Copy / Paste -----

        private void CopyItem(ClipboardItem item)
        {
            if (item == null) return;
            if (item.Type == ClipType.Image && item.ImageBase64 != null)
            {
                var bmp = ClipboardService.Base64ToBitmapSource(item.ImageBase64);
                if (bmp != null) ClipboardService.WriteImageSuppressed(bmp, _hwnd);
            }
            else if (item.TextContent != null)
            {
                ClipboardService.WriteTextSuppressed(item.TextContent, _hwnd);
            }
        }

        private static void Log(string msg)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "paste_debug.txt");
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\r\n");
            }
            catch { }
        }

        private void PasteItem(ClipboardItem item)
        {
            if (item == null) return;
            _vm.BringToTop(item); // move to top of history
            // Do NOT call CopyItem again — ClickCount=1 already wrote clipboard before ClickCount=2 fires.
            // Calling it again causes a race: EmptyClipboard removes CF_EXCLUDE right as
            // Windows Clipboard History checks the notification from write #1 → chw shows.
            var hwnd = _prevHwnd;
            Log($"PasteItem: prevHwnd={hwnd.ToInt64():X}, myHwnd={_hwnd.ToInt64():X}");

            // Transfer focus to target BEFORE hiding — we still have foreground rights now
            if (hwnd != IntPtr.Zero && hwnd != _hwnd)
            {
                if (Win32.IsIconic(hwnd)) Win32.ShowWindow(hwnd, 9); // SW_RESTORE only if minimized
                bool r = Win32.SetForegroundWindow(hwnd);
                Log($"SetForegroundWindow={r}");
            }
            else
            {
                Log("prevHwnd is zero or same as our window — skip SetForegroundWindow");
            }

            Hide();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var fg = Win32.GetForegroundWindow();
                try
                {
                    var clipText = System.Windows.Clipboard.GetText();
                    Log($"Clipboard at paste: '{(clipText?.Length > 40 ? clipText.Substring(0, 40) : clipText)}'");
                }
                catch (Exception ex2) { Log($"Clipboard read error: {ex2.Message}"); }
                Log($"Timer fired: fg={fg.ToInt64():X}");
                PasteService.SendCtrlV();
            };
            timer.Start();
        }

        // ----- Window events -----

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!_settingsOpen) Hide();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

        // ----- Tabs -----

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Primitives.ToggleButton)sender;
            TabAll.IsChecked = false;
            TabPinned.IsChecked = false;
            btn.IsChecked = true;
            _vm.ActiveTab = btn.Tag as string;
        }

        // ----- Search -----

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _vm.SearchText = SearchBox.Text;
        }

        // ----- Card click/double-click -----

        private void Card_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as ClipboardItem;
            if (item == null) return;
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                PasteItem(item);
            }
            else if (e.ClickCount == 1)
            {
                e.Handled = true;
                CopyItem(item);
            }
        }

        // ----- Card buttons -----

        private void CardPin_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).Tag is ClipboardItem item)
                _vm.TogglePin(item);
        }

        private void CardDelete_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).Tag is ClipboardItem item)
                _vm.DeleteItem(item);
        }

        // ----- Context menu -----

        private void CtxCopy_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.MenuItem)sender).Tag is ClipboardItem item)
                CopyItem(item);
        }

        private void CtxPaste_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.MenuItem)sender).Tag is ClipboardItem item)
                PasteItem(item);
        }

        private void CtxPin_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.MenuItem)sender).Tag is ClipboardItem item)
                _vm.TogglePin(item);
        }

        private void CtxDelete_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.MenuItem)sender).Tag is ClipboardItem item)
                _vm.DeleteItem(item);
        }

        // ----- Theme -----

        private void Theme_Click(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;
            ThemeBtn.Content = _isDark ? "🌙" : "☀";
            ApplyTheme(_isDark);
        }

        private void ApplyTheme(bool dark)
        {
            var res = System.Windows.Application.Current.Resources;
            if (dark)
            {
                res["BgBrush"]      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC141414"));
                res["CardBg"]       = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                res["CardBorder"]   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                res["CardHover"]    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
                res["TextPrimary"]  = new SolidColorBrush(Colors.White);
                res["TextSecondary"]= new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
                res["SearchBg"]     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
                res["TitleBarBg"]   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
                res["CardsAreaBg"]  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D0D0D"));
                res["SearchFg"]     = new SolidColorBrush(Colors.White);
            }
            else
            {
                res["BgBrush"]      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));
                res["CardBg"]       = new SolidColorBrush(Colors.White);
                res["CardBorder"]   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
                res["CardHover"]    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                res["TextPrimary"]  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111111"));
                res["TextSecondary"]= new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
                res["SearchBg"]     = new SolidColorBrush(Colors.White);
                res["TitleBarBg"]   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8"));
                res["CardsAreaBg"]  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                res["SearchFg"]     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111111"));
            }
        }

        // ----- Settings / Clear -----

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            _settingsOpen = true;
            var dlg = new SettingsWindow(_vm);
            dlg.Owner = this;
            dlg.ShowDialog();
            _settingsOpen = false;
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var r = System.Windows.MessageBox.Show(
                "Clear all unpinned items?", "Paste by Dan",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes) _vm.ClearAll();
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);
        public void Execute(object parameter) => _execute((T)parameter);
        public event EventHandler CanExecuteChanged;
    }
}
