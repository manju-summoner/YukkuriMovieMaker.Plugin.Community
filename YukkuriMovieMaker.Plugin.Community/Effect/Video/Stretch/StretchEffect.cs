using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Stretch
{
    [VideoEffect(nameof(Texts.StretchEffect), [VideoEffectCategories.Filtering], ["stretch"], isAviUtlSupported: false, ResourceType = typeof(Texts))]
    internal class StretchEffect : VideoEffectBase
    {
        public override string Label => Texts.StretchEffect;

        [Display(GroupName = nameof(Texts.StretchEffect), Name = nameof(Texts.X), Description = nameof(Texts.XDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.StretchEffect), Name = nameof(Texts.Y), Description = nameof(Texts.YDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.StretchEffect), Name = nameof(Texts.Angle), Description = nameof(Texts.AngleDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.StretchEffect), Name = nameof(Texts.StretchLength), Description = nameof(Texts.StretchLengthDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation StretchLength { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.StretchEffect), Name = nameof(Texts.Range), Description = nameof(Texts.RangeDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Range { get; } = new Animation(0, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.StretchEffect), Name = nameof(Texts.IsCentering), Description = nameof(Texts.IsCenteringDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsCentering { get => isCentering; set => Set(ref isCentering, value); }
        bool isCentering = true;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new StretchEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Angle, StretchLength, Range];
    }
}
