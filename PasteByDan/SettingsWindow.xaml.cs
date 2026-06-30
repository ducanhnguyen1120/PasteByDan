using System;
using System.IO;
using System.Windows;
using PasteByDan.ViewModels;

namespace PasteByDan
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _vm;

        public SettingsWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DarkThemeCheck.IsChecked = vm.IsDarkTheme;
            StorePath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PasteByDan", "store.json");
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
