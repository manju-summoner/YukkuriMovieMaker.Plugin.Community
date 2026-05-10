using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Seigaiha
{
    internal class SeigaihaBrushParameter : DrawingBrushParameterBase
    {
        [Display(GroupName = nameof(Texts.FlatColorGroup), Name = nameof(Texts.GradientEnabled), ResourceType = typeof(Texts), Order = 0)]
        [ToggleSlider]
        public bool GradientEnabled { get => gradientEnabled; set => Set(ref gradientEnabled, value); }
        bool gradientEnabled = false;

        [Display(GroupName = nameof(Texts.FlatColorGroup), Name = nameof(Texts.Color), ResourceType = typeof(Texts), Order = 1)]
        [ColorPicker]
        [FlatColorVisible]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(GroupName = nameof(Texts.GradientColorGroup), Name = nameof(Texts.OuterColor), ResourceType = typeof(Texts), Order = 2)]
        [ColorPicker]
        [GradientColorVisible]
        public Color OuterColor { get => outerColor; set => Set(ref outerColor, value); }
        Color outerColor = Colors.White;

        [Display(GroupName = nameof(Texts.GradientColorGroup), Name = nameof(Texts.InnerColor), ResourceType = typeof(Texts), Order = 3)]
        [ColorPicker]
        [GradientColorVisible]
        public Color InnerColor { get => innerColor; set => Set(ref innerColor, value); }
        Color innerColor = Colors.LightBlue;

        [Display(GroupName = nameof(Texts.CommonColorGroup), Name = nameof(Texts.BackgroundColor), ResourceType = typeof(Texts), Order = 4)]
        [ColorPicker]
        public Color BackgroundColor { get => backgroundColor; set => Set(ref backgroundColor, value); }
        Color backgroundColor = Colors.Black;

        [Display(GroupName = nameof(Texts.CommonColorGroup), Name = nameof(Texts.StrokeColor), ResourceType = typeof(Texts), Order = 5)]
        [ColorPicker]
        public Color StrokeColor { get => strokeColor; set => Set(ref strokeColor, value); }
        Color strokeColor = Colors.Gray;

        [Display(Name = nameof(Texts.Radius), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 1, 500)]
        public Animation Radius { get; } = new Animation(40, 1, YMM4Constants.MaximumBitmapSize / 4);

        [Display(Name = nameof(Texts.LineWidth), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 20)]
        public Animation LineWidth { get; } = new Animation(1.5, 0, 50);

        [Display(Name = nameof(Texts.RingCount), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 1, 10)]
        public Animation RingCount { get; } = new Animation(3, 1, 20);

        [Display(Name = nameof(Texts.Zoom), ResourceType = typeof(Texts), Order = 250)]
        [AnimationSlider("F1", "%", 1, 200)]
        public Animation Zoom { get; } = new Animation(100, 1, YMM4Constants.VeryLargeValue);

        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new SeigaihaBrushSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => base.GetAnimatables().Concat([Radius, LineWidth, RingCount, Zoom]);
    }
}
