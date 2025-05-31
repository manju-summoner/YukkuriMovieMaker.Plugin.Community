using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater.AudioSourceReader;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater
{
    public class AudioVolumeCalculater : Animatable, IFileItem
    {
        [Display(Name = nameof(Texts.AudioSourceType), Description = nameof(Texts.AudioSourceType), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public AudioSourceType SourceType { get => sourceType; set => Set(ref sourceType, value); }
        AudioSourceType sourceType = AudioSourceType.Timeline;

        [Display(AutoGenerateField = true)]
        public AudioSourceReaderParameterBase SourceParameter { get => sourceParameter; set => Set(ref sourceParameter, value); }
        AudioSourceReaderParameterBase sourceParameter = new TimelineAudioSourceReaderParameter();

        [Display(Name = nameof(Texts.Power), Description = nameof(Texts.Power), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Power { get; } = new Animation(100, 0, 99999);

        [Display(Name = nameof(Texts.Smooth), Description = nameof(Texts.Smooth), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 1, 10)]
        public Animation Smooth { get; } = new Animation(4, 1, 10);

        [Display(Name = nameof(Texts.Playback), Description = nameof(Texts.Playback), ResourceType = typeof(Texts))]
        [TimeSpanEditor]
        [TimeSpanDefaultValue("00:00:00.00")]
        public TimeSpan Playback { get => playback; set => Set(ref playback, value); }
        TimeSpan playback = TimeSpan.Zero;

        public AudioVolumeCalculaterSource CreateSource() => new(this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [SourceParameter, Power, Smooth];

        public override void BeginEdit()
        {
            base.BeginEdit();
        }

        public override ValueTask EndEditAsync()
        {
            SourceParameter = SourceType.Convert(SourceParameter);

            return base.EndEditAsync();
        }

        public IEnumerable<TimelineResource> GetResources()
        {
            if(SourceParameter is IResourceItem parameterResourceItem)
            {
                foreach(var resource in parameterResourceItem.GetResources())
                {
                    yield return resource;
                }
            }
        }

        public IEnumerable<string> GetFiles()
        {
            if (SourceParameter is IFileItem parameterFileItem)
            {
                foreach (var file in parameterFileItem.GetFiles())
                {
                    yield return file;
                }
            }
        }

        public void ReplaceFile(string from, string to)
        {
            if (SourceParameter is IFileItem parameterFileItem)
            {
                parameterFileItem.ReplaceFile(from, to);
            }
        }
    }
}
