using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.StringSpectrum
{
    public class StringSpectrumParameter(SharedDataStore? sharedData = null) : AudioSpectrumParameterBase(sharedData)
    {
        [Display(Name = nameof(Texts.StringWidth), Description = nameof(Texts.StringWidthDescription), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 1920)]
        public Animation StringWidth { get; } = new Animation(600, 0.01, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Amplitude), Description = nameof(Texts.AmplitudeDescription), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 400)]
        public Animation Amplitude { get; } = new Animation(120, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.BaseFrequency), Description = nameof(Texts.BaseFrequencyDescription), Order = 12, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0, 10)]
        public Animation BaseFrequency { get; } = new Animation(1.2, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.ModeLimit), Description = nameof(Texts.ModeLimitDescription), Order = 13, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 1, StringSpectrumCustomEffect.MaxModes)]
        public int ModeLimit { get => modeLimit; set => Set(ref modeLimit, Math.Clamp(value, 1, StringSpectrumCustomEffect.MaxModes)); }
        private int modeLimit = 24;

        [Display(Name = nameof(Texts.Thickness), Description = nameof(Texts.ThicknessDescription), Order = 14, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 30)]
        public Animation Thickness { get; } = new Animation(3, 0.01, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.StringColor), Description = nameof(Texts.StringColorDescription), Order = 15, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color StringColor { get => stringColor; set => Set(ref stringColor, value); }
        private Color stringColor = Colors.White;

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc, AudioSpectrumExoOutputDescription spectrumParameters) => [];

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters, AudioSpectrumExoOutputDescription spectrumParameters) => [];

        public override IAudioSpectrumSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new StringSpectrumSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
            [StringWidth, Amplitude, BaseFrequency, Thickness];

        protected override void LoadSharedData(SharedDataStore sharedData)
        {
            var data = sharedData.Load<StringSpectrumParameterData>();
            if (data is null)
                return;

            StringWidth.CopyFrom(data.StringWidth);
            Amplitude.CopyFrom(data.Amplitude);
            BaseFrequency.CopyFrom(data.BaseFrequency);
            Thickness.CopyFrom(data.Thickness);
            ModeLimit = data.ModeLimit;
            StringColor = data.StringColor;
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new StringSpectrumParameterData(this));
        }
    }

    public class StringSpectrumParameterData
    {
        public Animation StringWidth { get; } = new Animation(600, 0.01, YMM4Constants.VeryLargeValue);
        public Animation Amplitude { get; } = new Animation(120, 0, YMM4Constants.VeryLargeValue);
        public Animation BaseFrequency { get; } = new Animation(1.2, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);
        public Animation Thickness { get; } = new Animation(3, 0.01, YMM4Constants.VeryLargeValue);
        public int ModeLimit { get; set; }
        public Color StringColor { get; set; }

        public StringSpectrumParameterData(StringSpectrumParameter target)
        {
            StringWidth.CopyFrom(target.StringWidth);
            Amplitude.CopyFrom(target.Amplitude);
            BaseFrequency.CopyFrom(target.BaseFrequency);
            Thickness.CopyFrom(target.Thickness);
            ModeLimit = target.ModeLimit;
            StringColor = target.StringColor;
        }
    }
}
