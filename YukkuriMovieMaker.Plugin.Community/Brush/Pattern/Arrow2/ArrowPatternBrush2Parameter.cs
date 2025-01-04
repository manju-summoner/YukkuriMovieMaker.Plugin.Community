using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow2
{
    internal class ArrowPatternBrush2Parameter : DrawingBrushParameterBase
    {
        [Display(Name = nameof(Texts.Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(Name = nameof(Texts.BackgroundColor), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color BackgroundColor { get => backgroundColor; set => Set(ref backgroundColor, value); }
        Color backgroundColor = Colors.Black;

        [Display(Name = nameof(Texts.Width), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation Width { get; } = new Animation(50, 1, 99999);

        [Display(Name = nameof(Texts.Height), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation Height { get; } = new Animation(100, 1, 99999);

        [Display(Name = nameof(Texts.Point), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation Point { get; } = new Animation(50, 0, 99999);

        [Display(Name = nameof(Texts.Zoom), ResourceType = typeof(Texts), Order = 250)]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation Zoom { get; } = new Animation(100, 0, 5000);

        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new ArrowPatternBrush2Source(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([Width, Height, Point, Zoom]);
    }
}
