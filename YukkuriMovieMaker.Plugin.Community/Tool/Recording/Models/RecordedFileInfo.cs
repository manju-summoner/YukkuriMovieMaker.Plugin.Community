using System;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models
{
    public class RecordedFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public DateTime CreatedAt { get; set; }
        public long DataLength { get; set; }
    }
}



