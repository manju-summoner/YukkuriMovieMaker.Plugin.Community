using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow
{
    internal class ArrowPatternBrushParameter : BrushParameterBase
    {
        [Display(Name = nameof(Texts.Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(Name = nameof(Texts.BackgroundColor), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color BackgroundColor { get => backgroundColor; set => Set(ref backgroundColor, value); }
        Color backgroundColor = Colors.Black;

        [Display(Name = nameof(Texts.FeatherWidth), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation FeatherWidth { get; } = new Animation(50, 1, 99999);

        [Display(Name = nameof(Texts.ShaftWidth), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation ShaftWidth { get; } = new Animation(5, 0, 99999);

        [Display(Name = nameof(Texts.Height), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation Height { get; } = new Animation(100, 1, 99999);

        [Display(Name = nameof(Texts.Point), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation Point { get; } = new Animation(50, 0, 99999);

        [Display(Name = nameof(Texts.X), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0);

        [Display(Name = nameof(Texts.Y), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0);

        [Display(Name = nameof(Texts.Angle), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(0, -36000, 36000);

        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new ArrowPatternBrushSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => new[] { FeatherWidth, ShaftWidth, Height, Point, X, Y, Angle };
    }
}
