using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        private IntPtr _kbHook;
        private Win32.LowLevelKeyboardProc _kbProc;
        private readonly HashSet<uint> _pressedKeys = new HashSet<uint>();

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

            _kbProc = KeyboardHookProc;
            using (var proc = System.Diagnostics.Process.GetCurrentProcess())
            using (var mod = proc.MainModule)
                _kbHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _kbProc, Win32.GetModuleHandle(mod.ModuleName), 0);

            _winEventDelegate = new Win32.WinEventDelegate(OnForegroundChanged);
            _winEventHook = Win32.SetWinEventHook(
                Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                int msg = wParam.ToInt32();
                bool isDown = msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN;
                bool isUp   = msg == Win32.WM_KEYUP   || msg == Win32.WM_SYSKEYUP;

                if (isDown) _pressedKeys.Add(kb.vkCode);
                else if (isUp) _pressedKeys.Remove(kb.vkCode);

                if (isDown && !_settingsOpen)
                {
                    var mods    = _vm.HotkeyModVKs;
                    int trigger = _vm.HotkeyTriggerVK;
                    bool modsOk = mods.Count > 0 && mods.TrueForAll(m => _pressedKeys.Contains((uint)m));
                    if ((int)kb.vkCode == trigger && modsOk)
                    {
                        Dispatcher.InvokeAsync(ToggleWindow);
                        return (IntPtr)1; // suppress key
                    }
                }
            }
            return Win32.CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        protected override void OnClosed(EventArgs e)
        {
            Win32.RemoveClipboardFormatListener(_hwnd);
            if (_kbHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_kbHook);
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
            if (msg == 0x020E) // WM_MOUSEHWHEEL
            {
                int delta = (short)(wParam.ToInt64() >> 16);
                CardsScroll.ScrollToHorizontalOffset(CardsScroll.HorizontalOffset + delta);
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
            if (hwnd != IntPtr.Zero && hwnd != _hwnd)
                _prevHwnd = hwnd;
        }

        private void ToggleWindow()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                PositionWindow();
                Show();
                Activate();
                SearchBox.Focus();
            }
        }

        private void PositionWindow()
        {
            // Get DPI transform from our window (may be null before first show — fallback to 1.0)
            double m11 = 1.0, m22 = 1.0;
            var src = PresentationSource.FromVisual(this);
            if (src?.CompositionTarget != null)
            {
                m11 = src.CompositionTarget.TransformFromDevice.M11;
                m22 = src.CompositionTarget.TransformFromDevice.M22;
            }

            if (_prevHwnd != IntPtr.Zero)
            {
                var hMon = Win32.MonitorFromWindow(_prevHwnd, Win32.MONITOR_DEFAULTTONEAREST);
                var mi = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(Win32.MONITORINFO)) };
                if (Win32.GetMonitorInfo(hMon, ref mi))
                {
                    double w = (mi.rcWork.right  - mi.rcWork.left) * m11;
                    double h = (mi.rcWork.bottom - mi.rcWork.top)  * m22 * 0.27;
                    double l = mi.rcWork.left   * m11;
                    double t = mi.rcWork.bottom * m22 - h;
                    Width = w; Height = h; Left = l; Top = t;
                    return;
                }
            }

            var wa = SystemParameters.WorkArea;
            Width  = wa.Width;
            Height = wa.Height * 0.27;
            Left   = wa.Left;
            Top    = wa.Bottom - Height;
        }

        private void SetupTray()
        {
            _trayIcon = new NotifyIcon();
            try
            {
                var stream = System.Windows.Application.GetResourceStream(
                    new Uri("pack://application:,,,/Assets/tray.ico")).Stream;
                _trayIcon.Icon = new System.Drawing.Icon(stream);
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

        private void PasteItem(ClipboardItem item)
        {
            if (item == null) return;
            _vm.BringToTop(item); // move to top of history
            // Do NOT call CopyItem again — ClickCount=1 already wrote clipboard before ClickCount=2 fires.
            // Calling it again causes a race: EmptyClipboard removes CF_EXCLUDE right as
            // Windows Clipboard History checks the notification from write #1 → chw shows.
            var hwnd = _prevHwnd;

            if (hwnd != IntPtr.Zero && hwnd != _hwnd)
            {
                if (Win32.IsIconic(hwnd)) Win32.ShowWindow(hwnd, 9); // SW_RESTORE only if minimized
                Win32.SetForegroundWindow(hwnd);
            }

            Hide();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                PasteService.SendCtrlV();
            };
            timer.Start();
        }

        // ----- Window events -----

        private void CardsScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            CardsScroll.ScrollToHorizontalOffset(CardsScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

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
