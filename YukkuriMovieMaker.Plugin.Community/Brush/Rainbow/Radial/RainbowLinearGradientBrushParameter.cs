using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow.Radial
{
    internal class RainbowRadialGradientBrushParameter : BrushParameterBase
    {
        [Display(Name = nameof(Texts.Offset), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation Offset { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.CenterX), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation CenterX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.CenterY), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation CenterY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.OriginX), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation OriginX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.OriginY), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation OriginY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.RadiusX), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation RadiusX { get; } = new Animation(100, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.RadiusY), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation RadiusY { get; } = new Animation(100, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Zoom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 500)]
        public Animation Zoom { get; } = new Animation(100, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Angle), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Aspect), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation Aspect { get; } = new Animation(0, -100, 100);

        [Display(Name = nameof(Texts.Extend), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ExtendMode ExtendMode { get => extendMode; set => Set(ref extendMode, value); }
        ExtendMode extendMode = ExtendMode.Wrap;

        [Display(Name = nameof(Texts.ColorSpace), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public RainbowColorSpace ColorSpace { get => colorSpace; set => Set(ref colorSpace, value); }
        RainbowColorSpace colorSpace = RainbowColorSpace.HSV;

        [Display(Name = nameof(Texts.Saturation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Saturation { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Texts.Brightness), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Brightness { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Texts.IsInverted), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInverted { get => isInverted; set => Set(ref isInverted, value); }
        bool isInverted = false;

        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new RainbowRadialGradientBrushSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Offset, CenterX, CenterY, OriginX, OriginY, RadiusX, RadiusY, Zoom, Saturation, Brightness, Angle, Aspect];
    }
}