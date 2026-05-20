using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoiceParameter : VoiceParameterBase, IVoiceParameter
    {
        private string text = string.Empty;
        private string audioFilePath = string.Empty;

        [Display(Name = "セリフ", Description = "読み上げテキスト")]
        public string Text
        {
            get => text;
            set
            {
                text = value ?? string.Empty;
            }
        }

        [Display(Name = "録音ファイル", Description = "録音済み wav ファイルのパス")]
        [ReadOnly(true)]
        public string AudioFilePath
        {
            get => audioFilePath;
            set
            {
                audioFilePath = value ?? string.Empty;
                var exists = string.IsNullOrWhiteSpace(audioFilePath) ? "empty" : (System.IO.File.Exists(audioFilePath) ? "exists" : "missing");
            }
        }

        public TimeSpan? Duration { get; set; }
        public DateTime? CreatedAt { get; set; }

        public IVoiceParameter Clone()
        {
            return new RecordedVoiceParameter
            {
                Text = Text,
                AudioFilePath = AudioFilePath,
                Duration = Duration,
                CreatedAt = CreatedAt
            };
        }
    }
}


