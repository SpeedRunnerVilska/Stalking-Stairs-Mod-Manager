using System;

namespace StalkingStairsModManager.Models
{
    public class ModInfo
    {
        public string name { get; set; }
        public string author { get; set; }
        public string version { get; set; }
        public string downloadUrl { get; set; }
        public string description { get; set; }
        public bool enabled { get; set; }

        // Read-only combined display used by the UI: "Name - Version"
        public string DisplayName => string.IsNullOrWhiteSpace(version) ? name : $"{name} - {version}";
    }
}