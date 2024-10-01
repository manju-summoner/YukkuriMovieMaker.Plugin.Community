using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InnerOutline
{
    [VideoEffect(nameof(Texts.InnerOutlineEffectName), [VideoEffectCategories.Decoration], ["インナーアウトライン", "インナーボーダー", "inner outline", "inner border"], ResourceType = typeof(Texts))]
    public class InnerOutlineEffect : VideoEffectBase, IFileItem
    {
        public override string Label => $"{Texts.InnerOutlineEffectName} {Thickness.GetValue(0, 1, 30):F0}px";

        [Display(GroupName = nameof(Texts.InnerOutlineEffectName), Name = nameof(Texts.InnerOutlineEffectThicknessName), Description = nameof(Texts.InnerOutlineEffectThicknessDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Thickness { get; } = new Animation(3, 0, 500);

        [Display(GroupName = nameof(Texts.InnerOutlineEffectName), Name = nameof(Texts.InnerOutlineEffectOpacityName), Description = nameof(Texts.InnerOutlineEffectOpacityDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.InnerOutlineEffectName), Name = nameof(Texts.InnerOutlineEffectBlurName), Description = nameof(Texts.InnerOutlineEffectBlurDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 5)]
        public Animation Blur { get; } = new Animation(0, 0, 1000);

        [Display(GroupName = nameof(Texts.InnerOutlineEffectName), Name = nameof(Texts.InnerOutlineEffectBlendName), Description = nameof(Texts.InnerOutlineEffectBlendDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Blend Blend { get => blend; set => Set(ref blend, value); }
        Blend blend = Blend.Normal;

        [Display(GroupName = nameof(Texts.InnerOutlineEffectName), Name = nameof(Texts.InnerOutlineEffectIsOutlineOnlyName), Description = nameof(Texts.InnerOutlineEffectIsOutlineOnlyDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsOutlineOnly { get => isOutlineOnly; set => Set(ref isOutlineOnly, value); }
        bool isOutlineOnly = false;

        [Display(GroupName = nameof(Texts.InnerOutlineEffectBrushGroupName), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public Plugin.Brush.Brush Brush { get; } = new Plugin.Brush.Brush();

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new InnerOutlineEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Thickness, Opacity, Blur, Brush];

        public override IEnumerable<string> GetFiles()
        {
            foreach (var file in base.GetFiles())
                yield return file;
            foreach (var file in Brush.GetFiles())
                yield return file;
        }

        public override void ReplaceFile(string from, string to)
        {
            base.ReplaceFile(from, to);
            Brush.ReplaceFile(from, to);
        }

        public override IEnumerable<TimelineResource> GetResources()
        {
            foreach (var resource in base.GetResources())
                yield return resource;
            foreach (var resource in Brush.GetResources())
                yield return resource;
        }
    }
}
