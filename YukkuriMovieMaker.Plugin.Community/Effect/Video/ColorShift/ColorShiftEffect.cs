using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorShift
{
    [VideoEffect(nameof(Texts.ColorShiftEffectName), [VideoEffectCategories.Filtering], ["color shift", "color draft", "color registration error", "out of color registration", "色ずらし", "版ズレ", "版ずらし", "RGBズレ", "RGBずらし"], ResourceType = typeof(Texts))]

    public class ColorShiftEffect : VideoEffectBase
    {
        public override string Label => Texts.ColorShiftEffectName;

        [Display(GroupName = nameof(Texts.ColorShiftGroupName), Name = nameof(Texts.ColorShiftEffectShiftName), Description = nameof(Texts.ColorShiftEffectShiftDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 20d)]
        public Animation Shift { get; } = new Animation(5, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ColorShiftGroupName), Name = nameof(Texts.ColorShiftEffectAngleName), Description = nameof(Texts.ColorShiftEffectAngleDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360d, 360d)]
        public Animation Angle { get; } = new Animation(90, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ColorShiftGroupName), Name = nameof(Texts.ColorShiftEffectStrengthName), Description = nameof(Texts.ColorShiftEffectStrengthDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Strength { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.ColorShiftGroupName), Name = nameof(Texts.ColorShiftEffectModeName), Description = nameof(Texts.ColorShiftEffectModeDesc), Order = 100, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ColorShiftMode Mode { set => Set(ref mode, value); get => mode; }
        ColorShiftMode mode = ColorShiftMode.RBG;

        [Display(GroupName = nameof(Texts.ColorShiftGroupName), Name = nameof(Texts.ColorShiftEffectIsPremultipliedAlphaName), Description = nameof(Texts.ColorShiftEffectIsPremultipliedAlphaDesc), Order = 100, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsPremultipliedAlpha { set => Set(ref isPremultipliedAlpha, value); get => isPremultipliedAlpha; }
        bool isPremultipliedAlpha = true;


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            return
            [
                $"_name=色ずれ\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"ずれ幅={Shift.ToExoString(keyFrameIndex, "F1",fps)}\r\n" +
                $"角度={Angle.ToExoString(keyFrameIndex, "F1",fps)}\r\n" +
                $"強さ={Strength.ToExoString(keyFrameIndex, "F0",fps)}\r\n" +
                $"type={(int)Mode + (IsPremultipliedAlpha ? -1:2)}\r\n"
            ];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ColorShiftEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Shift, Angle, Strength];
    }
}
