using System;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models
{
    public class RecordingScriptItem
    {
        public string Text { get; set; } = string.Empty;
        public string AudioFilePath { get; set; } = string.Empty;
        public bool IsRecorded { get; set; }
        public TimeSpan? Duration { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}



