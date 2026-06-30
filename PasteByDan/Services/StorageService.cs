using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PasteByDan.Models;

namespace PasteByDan.Services
{
    public class AppData
    {
        public List<ClipboardItem> Items { get; set; } = new List<ClipboardItem>();
        public List<ClipGroup> Groups { get; set; } = new List<ClipGroup>();
        // VK codes: LCtrl=0xA2, RCtrl=0xA3, LShift=0xA0, RShift=0xA1, LAlt=0xA4, RAlt=0xA5, LWin=0x5B, RWin=0x5C
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<int> HotkeyModVKs { get; set; } = new List<int> { 0xA2, 0xA0 }; // LCtrl + LShift
        public int HotkeyTriggerVK { get; set; } = 0x43; // C
        public bool DarkTheme { get; set; } = true;
    }

    public static class StorageService
    {
        private static readonly string _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PasteByDan");
        private static readonly string _path = Path.Combine(_dir, "store.json");

        public static AppData Load()
        {
            try
            {
                if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
                if (!File.Exists(_path)) return new AppData();
                var json = File.ReadAllText(_path);
                return JsonConvert.DeserializeObject<AppData>(json) ?? new AppData();
            }
            catch { return new AppData(); }
        }

        public static void Save(AppData data)
        {
            try
            {
                if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_path, json);
            }
            catch { }
        }
    }
}
