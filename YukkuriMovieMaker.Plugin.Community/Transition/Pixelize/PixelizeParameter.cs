using System.ComponentModel.DataAnnotations;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YMM4SamplePlugin.Transition.Pixelize
{
    internal sealed class PixelizeParameter : TransitionParameterBase
    {
        [Display(Name = nameof(Texts.EasingTypeName), Description = nameof(Texts.EasingTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType { get => _easingType; set => Set(ref _easingType, value); }
        EasingType _easingType = EasingType.Expo;

        [Display(Name = nameof(Texts.EasingModeName), Description = nameof(Texts.EasingModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => _easingMode; set => Set(ref _easingMode, value); }
        EasingMode _easingMode = EasingMode.InOut;

        [Display(Name = nameof(Texts.MaxBlockSizeName), Description = nameof(Texts.MaxBlockSizeDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 4, 256)]
        public Animation MaxBlockSize { get; } = new Animation(64, 1, 512);

        public override ITransitionSource CreateTransition(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after)
            => new PixelizeSource(devices, before, after, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [MaxBlockSize];
    }
}
