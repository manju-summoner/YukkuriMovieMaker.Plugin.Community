using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoiceParameter : VoiceParameterBase, IVoiceParameter
    {
        private string text = string.Empty;
        private string recordsDirectory = string.Empty;
        private string audioFilePath = string.Empty;
        private TimeSpan? duration;
        private DateTime? createdAt;

        [Display(Name = nameof(Texts.ParameterTextName), Description = nameof(Texts.ParameterTextDescription), ResourceType = typeof(Texts))]
        public string Text
        {
            get => text;
            set => Set(ref text, value ?? string.Empty);
        }

        [Browsable(false)]
        public string RecordsDirectory
        {
            get => recordsDirectory;
            set => Set(ref recordsDirectory, value ?? string.Empty);
        }

        [Display(Name = nameof(Texts.ParameterAudioFileName), Description = nameof(Texts.ParameterAudioFileDescription), ResourceType = typeof(Texts))]
        [FileSelector(FileGroupType.None)]
        public string AudioFilePath
        {
            get => audioFilePath;
            set => Set(ref audioFilePath, value ?? string.Empty);
        }

        [Browsable(false)]
        public TimeSpan? Duration
        {
            get => duration;
            set => Set(ref duration, value);
        }

        [Browsable(false)]
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
                RecordsDirectory = RecordsDirectory,
                AudioFilePath = AudioFilePath,
                Duration = Duration,
                CreatedAt = CreatedAt
            };
        }
    }
}
