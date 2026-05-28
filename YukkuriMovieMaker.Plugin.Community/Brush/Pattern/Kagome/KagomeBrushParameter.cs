using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Kagome
{
    internal class KagomeBrushParameter : DrawingBrushParameterBase
    {
        [Display(Name = nameof(Texts.WeaveEnabled), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool WeaveEnabled { get => weaveEnabled; set => Set(ref weaveEnabled, value); }
        bool weaveEnabled = true;

        [Display(Name = nameof(Texts.Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(Name = nameof(Texts.OutlineColor), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color OutlineColor { get => outlineColor; set => Set(ref outlineColor, value); }
        Color outlineColor = Colors.Gray;

        [Display(Name = nameof(Texts.BackgroundColor), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color BackgroundColor { get => backgroundColor; set => Set(ref backgroundColor, value); }
        Color backgroundColor = Colors.Black;

        [Display(Name = nameof(Texts.Size), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 1, 500)]
        public Animation Size { get; } = new Animation(60, 1, YMM4Constants.MaximumBitmapSize / 2);

        [Display(Name = nameof(Texts.LineWidth), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0.1, 30)]
        public Animation LineWidth { get; } = new Animation(8, 0.1, YMM4Constants.MaximumBitmapSize / 2);

        [Display(Name = nameof(Texts.OutlineWidth), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 20)]
        public Animation OutlineWidth { get; } = new Animation(2, 0, YMM4Constants.MaximumBitmapSize / 2);

        [Display(Name = nameof(Texts.Zoom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 1, 200)]
        public Animation Zoom { get; } = new Animation(100, 1, YMM4Constants.VeryLargeValue);

        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new KagomeBrushSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => base.GetAnimatables().Concat([Size, LineWidth, OutlineWidth, Zoom]);
    }
}
