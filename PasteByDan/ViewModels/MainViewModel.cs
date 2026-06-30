using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using PasteByDan.Models;
using PasteByDan.Services;

namespace PasteByDan.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private AppData _data;
        private string _searchText = "";
        private string _activeTab = "All"; // All, Pinned, Collections
        private string _activeGroupId = null;
        private bool _isDarkTheme = true;

        public ObservableCollection<ClipboardItem> FilteredItems { get; } = new ObservableCollection<ClipboardItem>();
        public ObservableCollection<ClipGroup> Groups { get; } = new ObservableCollection<ClipGroup>();

        // Set by MainWindow after construction
        public System.Windows.Input.ICommand CopyCommand { get; set; }
        public System.Windows.Input.ICommand PasteCommand { get; set; }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); RefreshFilter(); }
        }

        public string ActiveTab
        {
            get => _activeTab;
            set { _activeTab = value; OnPropertyChanged(); _activeGroupId = null; RefreshFilter(); }
        }

        public string ActiveGroupId
        {
            get => _activeGroupId;
            set { _activeGroupId = value; _activeTab = "Collections"; OnPropertyChanged(); RefreshFilter(); }
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set { _isDarkTheme = value; OnPropertyChanged(); _data.DarkTheme = value; SaveData(); }
        }

        public List<int> HotkeyModVKs
        {
            get => _data.HotkeyModVKs;
            set { _data.HotkeyModVKs = value; SaveData(); OnPropertyChanged(); }
        }

        public int HotkeyTriggerVK
        {
            get => _data.HotkeyTriggerVK;
            set { _data.HotkeyTriggerVK = value; SaveData(); OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _data = StorageService.Load();
            _isDarkTheme = _data.DarkTheme;

            foreach (var g in _data.Groups) Groups.Add(g);
            RefreshFilter();
        }

        public void AddItem(ClipboardItem item)
        {
            // Skip duplicate of most recent
            if (_data.Items.Count > 0)
            {
                var last = _data.Items[0];
                if (last.Type == item.Type && last.TextContent == item.TextContent &&
                    last.ImageBase64 == item.ImageBase64)
                    return;
            }

            _data.Items.Insert(0, item);

            // Enforce limits
            var textItems = _data.Items.Where(x => x.Type != ClipType.Image).ToList();
            var imgItems = _data.Items.Where(x => x.Type == ClipType.Image).ToList();
            while (textItems.Count(x => !x.IsPinned) > 1000)
            {
                var oldest = textItems.Where(x => !x.IsPinned).Last();
                _data.Items.Remove(oldest);
                textItems.Remove(oldest);
            }
            while (imgItems.Count(x => !x.IsPinned) > 300)
            {
                var oldest = imgItems.Where(x => !x.IsPinned).Last();
                _data.Items.Remove(oldest);
                imgItems.Remove(oldest);
            }

            RefreshFilter();
            SaveData();
        }

        public void BringToTop(ClipboardItem item)
        {
            item.Timestamp = DateTime.Now;
            RefreshFilter();
            SaveData();
        }

        public void TogglePin(ClipboardItem item)
        {
            item.IsPinned = !item.IsPinned;
            RefreshFilter();
            SaveData();
        }

        public void DeleteItem(ClipboardItem item)
        {
            _data.Items.Remove(item);
            RefreshFilter();
            SaveData();
        }

        public void ClearAll()
        {
            _data.Items.RemoveAll(x => !x.IsPinned);
            RefreshFilter();
            SaveData();
        }

        public ClipGroup AddGroup(string name)
        {
            var g = new ClipGroup { Name = name };
            _data.Groups.Add(g);
            Groups.Add(g);
            SaveData();
            return g;
        }

        public void RenameGroup(ClipGroup group, string newName)
        {
            group.Name = newName;
            var idx = Groups.IndexOf(group);
            if (idx >= 0) { Groups.RemoveAt(idx); Groups.Insert(idx, group); }
            SaveData();
        }

        public void DeleteGroup(ClipGroup group)
        {
            foreach (var item in _data.Items.Where(x => x.GroupId == group.Id))
                item.GroupId = null;
            _data.Groups.Remove(group);
            Groups.Remove(group);
            if (_activeGroupId == group.Id) ActiveGroupId = null;
            RefreshFilter();
            SaveData();
        }

        public void MoveToGroup(ClipboardItem item, string groupId)
        {
            item.GroupId = groupId;
            SaveData();
        }

        public void RefreshFilter()
        {
            FilteredItems.Clear();

            IOrderedEnumerable<ClipboardItem> ordered = _data.Items
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.Timestamp);

            foreach (var item in ordered)
            {
                bool matchesTab = _activeTab == "All" ||
                    (_activeTab == "Pinned" && item.IsPinned) ||
                    (_activeTab == "Collections" && (_activeGroupId == null ?
                        !string.IsNullOrEmpty(item.GroupId) :
                        item.GroupId == _activeGroupId));

                bool matchesSearch = string.IsNullOrEmpty(_searchText) ||
                    (item.TextContent != null && item.TextContent.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0);

                if (matchesTab && matchesSearch)
                    FilteredItems.Add(item);
            }
        }

        private void SaveData() => StorageService.Save(_data);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
