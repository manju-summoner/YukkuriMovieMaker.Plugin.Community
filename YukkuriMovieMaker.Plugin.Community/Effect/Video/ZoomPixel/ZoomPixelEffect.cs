using System.ComponentModel.DataAnnotations;
using System.Text;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size.Parameter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel
{
    [VideoEffect(nameof(Texts.ZoomPixel), [VideoEffectCategories.Drawing], ["pixel zoom", "resize"], IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class ZoomPixelEffect : VideoEffectBase
    {
        public override string Label => Texts.ZoomPixel + " " + Size.Label;

        [Display(GroupName = nameof(Texts.ZoomPixel), Name = nameof(Texts.Mode), Description = nameof(Texts.Mode), ResourceType = typeof(Texts))]
        [EnumComboBox(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public SizeMode SizeMode { get => sizeMode; set => Set(ref sizeMode, value); }
        SizeMode sizeMode = SizeMode.BothStretch;

        [Display(AutoGenerateField = true)]
        public SizeParameterBase Size { get => size; set => Set(ref size, value); }
        SizeParameterBase size = new BothStretchParameter();

        [Display(GroupName = nameof(Texts.ZoomPixel), Name = nameof(Dot), Description = nameof(Dot), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Dot {  get => dot; set => Set(ref dot, value); }
        bool dot = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return Size.CreateExoVideoFilters(IsEnabled, keyFrameIndex, exoOutputDescription, Dot);
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ZoomPixelEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Size];

        public override void BeginEdit()
        {
            base.BeginEdit();
        }

        public override ValueTask EndEditAsync()
        {
            Size = SizeMode.Convert(Size);

            return base.EndEditAsync();
        }
    }
}
