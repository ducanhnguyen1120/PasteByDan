using System;

namespace PasteByDan.Models
{
    public class ClipGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Color { get; set; } = "#5B8AF5";
    }
}
