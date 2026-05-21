using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoiceParameter : VoiceParameterBase, IVoiceParameter
    {
        private string text = string.Empty;
        private string audioFilePath = string.Empty;
        private TimeSpan? duration;
        private DateTime? createdAt;

        [Display(Name = "セリフ", Description = "読み上げテキスト")]
        public string Text
        {
            get => text;
            set => Set(ref text, value ?? string.Empty);
        }

        [Display(Name = "録音ファイル", Description = "録音済み wav ファイルのパス")]
        [ReadOnly(true)]
        public string AudioFilePath
        {
            get => audioFilePath;
            set => Set(ref audioFilePath, value ?? string.Empty);
        }

        public TimeSpan? Duration
        {
            get => duration;
            set => Set(ref duration, value);
        }

        public DateTime? CreatedAt
        {
            get => createdAt;
            set => Set(ref createdAt, value);
        }

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
