using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PasteByDan.ViewModels;

namespace PasteByDan
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _recording = false;
        private readonly HashSet<Key> _heldModifiers = new HashSet<Key>();

        private static readonly HashSet<Key> ModifierKeys = new HashSet<Key>
        {
            Key.LeftCtrl, Key.RightCtrl,
            Key.LeftShift, Key.RightShift,
            Key.LeftAlt, Key.RightAlt,
            Key.LWin, Key.RWin
        };

        private static readonly Dictionary<int, string> VkNames = new Dictionary<int, string>
        {
            [0xA0] = "LShift", [0xA1] = "RShift",
            [0xA2] = "LCtrl",  [0xA3] = "RCtrl",
            [0xA4] = "LAlt",   [0xA5] = "RAlt",
            [0x5B] = "LWin",   [0x5C] = "RWin",
        };

        public static string VkToName(int vk)
        {
            if (VkNames.TryGetValue(vk, out var name)) return name;
            var key = KeyInterop.KeyFromVirtualKey(vk);
            return key == Key.None ? $"0x{vk:X2}" : key.ToString();
        }

        public static string BuildHotkeyLabel(List<int> modVks, int triggerVk)
        {
            var parts = new List<string>();
            foreach (var m in modVks) parts.Add(VkToName(m));
            parts.Add(VkToName(triggerVk));
            return string.Join(" + ", parts);
        }

        public SettingsWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DarkThemeCheck.IsChecked = vm.IsDarkTheme;
            HotkeyLabel.Text = BuildHotkeyLabel(vm.HotkeyModVKs, vm.HotkeyTriggerVK);
            StorePath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PasteByDan", "store.json");
        }

        private void RecordBtn_Click(object sender, RoutedEventArgs e)
        {
            _recording = true;
            _heldModifiers.Clear();
            RecordBtn.Content = "Cancel";
            RecordHint.Visibility = Visibility.Visible;
            HotkeyLabel.Text = "...";
            Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_recording) return;
            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                CancelRecording();
                return;
            }

            if (ModifierKeys.Contains(key))
            {
                _heldModifiers.Add(key);
                // Show live preview
                var preview = new List<string>();
                foreach (var m in _heldModifiers) preview.Add(KeyToName(m));
                HotkeyLabel.Text = string.Join(" + ", preview) + " + ...";
                return;
            }

            // Non-modifier key = trigger
            if (_heldModifiers.Count == 0)
            {
                HotkeyLabel.Text = "Need at least one modifier!";
                return;
            }

            var modVks = new List<int>();
            foreach (var m in _heldModifiers) modVks.Add(KeyInterop.VirtualKeyFromKey(m));
            int triggerVk = KeyInterop.VirtualKeyFromKey(key);

            _vm.HotkeyModVKs = modVks;
            _vm.HotkeyTriggerVK = triggerVk;
            HotkeyLabel.Text = BuildHotkeyLabel(modVks, triggerVk);

            StopRecording();
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (!_recording) return;
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            _heldModifiers.Remove(key);
        }

        private void StopRecording()
        {
            _recording = false;
            RecordBtn.Content = "Change";
            RecordHint.Visibility = Visibility.Collapsed;
        }

        private void CancelRecording()
        {
            HotkeyLabel.Text = BuildHotkeyLabel(_vm.HotkeyModVKs, _vm.HotkeyTriggerVK);
            StopRecording();
        }

        private static string KeyToName(Key key)
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            return VkToName(vk);
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            _vm.IsDarkTheme = DarkThemeCheck.IsChecked == true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
