using System;
using System.Threading.Tasks;
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

        private const int HOTKEY_ID = 9001;
        private const uint MOD_CTRL_SHIFT = Win32.MOD_CONTROL | Win32.MOD_SHIFT;

        public ICommand CopyCommand { get; }
        public ICommand PasteCommand { get; }

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            CopyCommand = new RelayCommand<ClipboardItem>(CopyItem);
            PasteCommand = new RelayCommand<ClipboardItem>(PasteItem);

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

        private void ToggleWindow()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                _prevHwnd = Win32.GetForegroundWindow();
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

        private async void PasteItem(ClipboardItem item)
        {
            if (item == null) return;
            CopyItem(item);
            Hide();
            // Focus previous window from UI thread — has message loop so AttachThreadInput works
            PasteService.FocusWindow(_prevHwnd);
            await Task.Delay(200);
            PasteService.SendCtrlV();
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
