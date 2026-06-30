using System;
using Newtonsoft.Json;

namespace PasteByDan.Models
{
    public enum ClipType { Text, Link, Email, Phone, Code, Image }

    public class ClipboardItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ClipType Type { get; set; }
        public string TextContent { get; set; }
        // Image stored as base64 PNG
        public string ImageBase64 { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
        public string GroupId { get; set; }

        [JsonIgnore]
        public string Preview
        {
            get
            {
                if (Type == ClipType.Image) return "[Image]";
                if (string.IsNullOrEmpty(TextContent)) return "";
                return TextContent.Length > 200 ? TextContent.Substring(0, 200) : TextContent;
            }
        }

        [JsonIgnore]
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalSeconds < 60) return "just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                return $"{(int)diff.TotalDays}d ago";
            }
        }

        public static ClipType DetectType(string text)
        {
            if (string.IsNullOrEmpty(text)) return ClipType.Text;
            var t = text.Trim();
            if (t.StartsWith("http://") || t.StartsWith("https://") || t.StartsWith("www."))
                return ClipType.Link;
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return ClipType.Email;
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[\+\d\s\-\(\)]{7,20}$"))
                return ClipType.Phone;
            if (t.Contains("{") || t.Contains("(") && t.Contains(")") || t.Contains("=>") ||
                t.Contains("function") || t.Contains("class ") || t.Contains("def ") ||
                t.Contains("import ") || t.Contains("var ") || t.Contains("const ") || t.Contains("let "))
                return ClipType.Code;
            return ClipType.Text;
        }
    }
}
