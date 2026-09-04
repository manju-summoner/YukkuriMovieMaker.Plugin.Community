using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.RippleSpectrum
{
    public class RippleSpectrumParameter(SharedDataStore? sharedData = null) : AudioSpectrumParameterBase(sharedData)
    {
        [Display(Name = nameof(Texts.InnerRadius), Description = nameof(Texts.InnerRadiusDescription), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation InnerRadius { get; } = new Animation(40, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Reach), Description = nameof(Texts.ReachDescription), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 800)]
        public Animation Reach { get; } = new Animation(260, 0.01, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Speed), Description = nameof(Texts.SpeedDescription), Order = 12, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0, 3)]
        public Animation Speed { get; } = new Animation(0.35, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.MinThickness), Description = nameof(Texts.MinThicknessDescription), Order = 13, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 30)]
        public Animation MinThickness { get; } = new Animation(1, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.MaxThickness), Description = nameof(Texts.MaxThicknessDescription), Order = 14, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 60)]
        public Animation MaxThickness { get; } = new Animation(10, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.ValueFollow), Description = nameof(Texts.ValueFollowDescription), Order = 15, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ValueFollow { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Texts.Decay), Description = nameof(Texts.DecayDescription), Order = 16, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Decay { get; } = new Animation(70, 0, 100);

        [Display(Name = nameof(Texts.RippleColor), Description = nameof(Texts.RippleColorDescription), Order = 17, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color RippleColor { get => rippleColor; set => Set(ref rippleColor, value); }
        private Color rippleColor = Colors.White;

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc, AudioSpectrumExoOutputDescription spectrumParameters) => [];

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters, AudioSpectrumExoOutputDescription spectrumParameters) => [];

        public override IAudioSpectrumSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new RippleSpectrumSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
            [InnerRadius, Reach, Speed, MinThickness, MaxThickness, ValueFollow, Decay];

        protected override void LoadSharedData(SharedDataStore sharedData)
        {
            var data = sharedData.Load<RippleSpectrumParameterData>();
            if (data is null)
                return;

            InnerRadius.CopyFrom(data.InnerRadius);
            Reach.CopyFrom(data.Reach);
            Speed.CopyFrom(data.Speed);
            MinThickness.CopyFrom(data.MinThickness);
            MaxThickness.CopyFrom(data.MaxThickness);
            ValueFollow.CopyFrom(data.ValueFollow);
            Decay.CopyFrom(data.Decay);
            RippleColor = data.RippleColor;
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new RippleSpectrumParameterData(this));
        }
    }

    public class RippleSpectrumParameterData
    {
        public Animation InnerRadius { get; } = new Animation(40, 0, YMM4Constants.VeryLargeValue);
        public Animation Reach { get; } = new Animation(260, 0.01, YMM4Constants.VeryLargeValue);
        public Animation Speed { get; } = new Animation(0.35, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);
        public Animation MinThickness { get; } = new Animation(1, 0, YMM4Constants.VeryLargeValue);
        public Animation MaxThickness { get; } = new Animation(10, 0, YMM4Constants.VeryLargeValue);
        public Animation ValueFollow { get; } = new Animation(100, 0, 100);
        public Animation Decay { get; } = new Animation(70, 0, 100);
        public Color RippleColor { get; set; }

        public RippleSpectrumParameterData(RippleSpectrumParameter target)
        {
            InnerRadius.CopyFrom(target.InnerRadius);
            Reach.CopyFrom(target.Reach);
            Speed.CopyFrom(target.Speed);
            MinThickness.CopyFrom(target.MinThickness);
            MaxThickness.CopyFrom(target.MaxThickness);
            ValueFollow.CopyFrom(target.ValueFollow);
            Decay.CopyFrom(target.Decay);
            RippleColor = target.RippleColor;
        }
    }
}
