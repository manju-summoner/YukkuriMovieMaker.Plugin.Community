using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.LensFlare
{
    public class LensFlareShapeParameter(SharedDataStore sharedData) : ShapeParameterBase(sharedData)
    {
        [Display(Name = nameof(Texts.LensFlareShapeParameterXName), Description = nameof(Texts.LensFlareShapeParameterXDesc), Order = 31, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation X { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterYName), Description = nameof(Texts.LensFlareShapeParameterYDesc), Order = 32, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation Y { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterIntensityName), Description = nameof(Texts.LensFlareShapeParameterIntensityDesc), Order = 33, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation Intensity { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterScaleName), Description = nameof(Texts.LensFlareShapeParameterScaleDesc), Order = 34, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation Scale { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterColorName), Description = nameof(Texts.LensFlareShapeParameterColorDesc), Order = 35, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color LightColor { set => Set(ref lightColor, value); get => lightColor; }
        Color lightColor = Colors.White;

        [Display(Name = nameof(Texts.LensFlareShapeParameterBladeCountName), Description = nameof(Texts.LensFlareShapeParameterBladeCountDesc), Order = 36, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 3, 16)]
        public Animation BladeCount { get; } = new Animation(6, 3, 32);

        [Display(Name = nameof(Texts.LensFlareShapeParameterRotationName), Description = nameof(Texts.LensFlareShapeParameterRotationDesc), Order = 37, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Rotation { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterStarLengthName), Description = nameof(Texts.LensFlareShapeParameterStarLengthDesc), Order = 38, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation StarLength { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterStarBrightnessName), Description = nameof(Texts.LensFlareShapeParameterStarBrightnessDesc), Order = 39, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation StarBrightness { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterGhostCountName), Description = nameof(Texts.LensFlareShapeParameterGhostCountDesc), Order = 40, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 0, 24)]
        public Animation GhostCount { get; } = new Animation(12, 0, 24);

        [Display(Name = nameof(Texts.LensFlareShapeParameterGhostBrightnessName), Description = nameof(Texts.LensFlareShapeParameterGhostBrightnessDesc), Order = 41, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation GhostBrightness { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterHaloRadiusName), Description = nameof(Texts.LensFlareShapeParameterHaloRadiusDesc), Order = 42, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation HaloRadius { get; } = new Animation(60, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterHaloBrightnessName), Description = nameof(Texts.LensFlareShapeParameterHaloBrightnessDesc), Order = 43, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation HaloBrightness { get; } = new Animation(50, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterDispersionName), Description = nameof(Texts.LensFlareShapeParameterDispersionDesc), Order = 44, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Dispersion { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.LensFlareShapeParameterSeedName), Description = nameof(Texts.LensFlareShapeParameterSeedDesc), Order = 45, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 0, 100)]
        public Animation Seed { get; } = new Animation(0, 0, YMM4Constants.VeryLargeValue);

        [Obsolete("JsonSerializer用")]
        public LensFlareShapeParameter() : this(null!) { }

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc) => [];
        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters) => [];

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new LensFlareShapeParameterSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
            [X, Y, Intensity, Scale, BladeCount, Rotation, StarLength, StarBrightness, GhostCount, GhostBrightness, HaloRadius, HaloBrightness, Dispersion, Seed];

        protected override void LoadSharedData(SharedDataStore sharedData)
        {
            var data = sharedData.Load<LensFlareShapeParameterData>();
            if (data is null) return;
            X.CopyFrom(data.X);
            Y.CopyFrom(data.Y);
            Intensity.CopyFrom(data.Intensity);
            Scale.CopyFrom(data.Scale);
            LightColor = data.LightColor;
            BladeCount.CopyFrom(data.BladeCount);
            Rotation.CopyFrom(data.Rotation);
            StarLength.CopyFrom(data.StarLength);
            StarBrightness.CopyFrom(data.StarBrightness);
            GhostCount.CopyFrom(data.GhostCount);
            GhostBrightness.CopyFrom(data.GhostBrightness);
            HaloRadius.CopyFrom(data.HaloRadius);
            HaloBrightness.CopyFrom(data.HaloBrightness);
            Dispersion.CopyFrom(data.Dispersion);
            Seed.CopyFrom(data.Seed);
        }

        protected override void SaveSharedData(SharedDataStore storage)
        {
            storage.Save(new LensFlareShapeParameterData(this));
        }
    }

    public class LensFlareShapeParameterData
    {
        public Animation X { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);
        public Animation Y { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);
        public Animation Intensity { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Animation Scale { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Color LightColor { set; get; }
        public Animation BladeCount { get; } = new Animation(6, 3, 32);
        public Animation Rotation { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);
        public Animation StarLength { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Animation StarBrightness { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Animation GhostCount { get; } = new Animation(12, 0, 24);
        public Animation GhostBrightness { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Animation HaloRadius { get; } = new Animation(60, 0, YMM4Constants.VeryLargeValue);
        public Animation HaloBrightness { get; } = new Animation(50, 0, YMM4Constants.VeryLargeValue);
        public Animation Dispersion { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Animation Seed { get; } = new Animation(0, 0, YMM4Constants.VeryLargeValue);

        public LensFlareShapeParameterData(LensFlareShapeParameter target)
        {
            X.CopyFrom(target.X);
            Y.CopyFrom(target.Y);
            Intensity.CopyFrom(target.Intensity);
            Scale.CopyFrom(target.Scale);
            LightColor = target.LightColor;
            BladeCount.CopyFrom(target.BladeCount);
            Rotation.CopyFrom(target.Rotation);
            StarLength.CopyFrom(target.StarLength);
            StarBrightness.CopyFrom(target.StarBrightness);
            GhostCount.CopyFrom(target.GhostCount);
            GhostBrightness.CopyFrom(target.GhostBrightness);
            HaloRadius.CopyFrom(target.HaloRadius);
            HaloBrightness.CopyFrom(target.HaloBrightness);
            Dispersion.CopyFrom(target.Dispersion);
            Seed.CopyFrom(target.Seed);
        }
    }
}
