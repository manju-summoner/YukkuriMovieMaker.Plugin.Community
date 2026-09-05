using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.MetaballSpectrum
{
    public class MetaballSpectrumParameter(SharedDataStore? sharedData = null) : AudioSpectrumParameterBase(sharedData)
    {
        [Display(Name = nameof(Texts.FieldWidth), Description = nameof(Texts.FieldWidthDescription), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 1920)]
        public Animation FieldWidth { get; } = new Animation(600, 0.01, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.FieldHeight), Description = nameof(Texts.FieldHeightDescription), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 1080)]
        public Animation FieldHeight { get; } = new Animation(300, 0.01, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.BlobRadius), Description = nameof(Texts.BlobRadiusDescription), Order = 12, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 1, 120)]
        public Animation BlobRadius { get; } = new Animation(36, 0.01, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Threshold), Description = nameof(Texts.ThresholdDescription), Order = 13, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 5, 95)]
        public Animation Threshold { get; } = new Animation(45, 0.1, 99.9);

        [Display(Name = nameof(Texts.IsBipolar), Description = nameof(Texts.IsBipolarDescription), Order = 14, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsBipolar { get => isBipolar; set => Set(ref isBipolar, value); }
        private bool isBipolar = false;

        [Display(Name = nameof(Texts.MetaballColor), Description = nameof(Texts.MetaballColorDescription), Order = 15, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color MetaballColor { get => metaballColor; set => Set(ref metaballColor, value); }
        private Color metaballColor = Colors.White;

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc, AudioSpectrumExoOutputDescription spectrumParameters) => [];

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters, AudioSpectrumExoOutputDescription spectrumParameters) => [];

        public override IAudioSpectrumSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new MetaballSpectrumSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
            [FieldWidth, FieldHeight, BlobRadius, Threshold];

        protected override void LoadSharedData(SharedDataStore sharedData)
        {
            var data = sharedData.Load<MetaballSpectrumParameterData>();
            if (data is null)
                return;

            FieldWidth.CopyFrom(data.FieldWidth);
            FieldHeight.CopyFrom(data.FieldHeight);
            BlobRadius.CopyFrom(data.BlobRadius);
            Threshold.CopyFrom(data.Threshold);
            IsBipolar = data.IsBipolar;
            MetaballColor = data.MetaballColor;
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new MetaballSpectrumParameterData(this));
        }
    }

    public class MetaballSpectrumParameterData
    {
        public Animation FieldWidth { get; } = new Animation(600, 0.01, YMM4Constants.VeryLargeValue);
        public Animation FieldHeight { get; } = new Animation(300, 0.01, YMM4Constants.VeryLargeValue);
        public Animation BlobRadius { get; } = new Animation(36, 0.01, YMM4Constants.VeryLargeValue);
        public Animation Threshold { get; } = new Animation(45, 0.1, 99.9);
        public bool IsBipolar { get; set; }
        public Color MetaballColor { get; set; }

        public MetaballSpectrumParameterData(MetaballSpectrumParameter target)
        {
            FieldWidth.CopyFrom(target.FieldWidth);
            FieldHeight.CopyFrom(target.FieldHeight);
            BlobRadius.CopyFrom(target.BlobRadius);
            Threshold.CopyFrom(target.Threshold);
            IsBipolar = target.IsBipolar;
            MetaballColor = target.MetaballColor;
        }
    }
}
