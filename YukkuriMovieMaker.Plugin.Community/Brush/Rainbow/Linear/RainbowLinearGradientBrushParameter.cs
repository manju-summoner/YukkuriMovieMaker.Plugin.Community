using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Brush.Commons;
using YukkuriMovieMaker.Plugin.Community.Brush.RainbowLinearGradient;
using YukkuriMovieMaker.Plugin.Community.Commons;
using Texts = YukkuriMovieMaker.Plugin.Community.Brush.RainbowLinearGradient.Texts;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow.Linear
{
    internal class RainbowLinearGradientBrushParameter : BrushParameterBase
    {
        [Display(Name = nameof(Texts.Width), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Width { get; } = new Animation(100);

        [Display(Name = nameof(Texts.Offset), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Offset { get; } = new Animation(0);

        [Display(Name = nameof(Texts.Angle), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(0, -36000, 36000);

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



        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new RainbowLinearGradientBrushSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Width, Offset, Saturation, Brightness, Angle];
    }
}