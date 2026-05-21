using System;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class RecordingScriptItemService
    {
        public void ApplyRecorded(RecordingScriptItem item, string text, string audioFilePath, TimeSpan duration, DateTime createdAt)
        {
            item.AudioFilePath = audioFilePath;
            item.Text = text;
            item.Duration = duration;
            item.CreatedAt = createdAt;
            item.IsRecorded = true;
        }

        public void ApplySilentPlaceholder(RecordingScriptItem item, string audioFilePath, TimeSpan duration, DateTime createdAt)
        {
            item.AudioFilePath = audioFilePath;
            item.Duration = duration;
            item.CreatedAt = createdAt;
            item.IsRecorded = false;
        }
    }
}
