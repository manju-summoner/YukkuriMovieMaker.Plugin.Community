using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InOutCrop
{
    [VideoEffect(nameof(Texts.InOutCropEffectName), [VideoEffectCategories.Transition], ["clipping", "crop", "クロップ"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class InOutCropEffect : VideoEffectBase
    {
        public override string Label => Texts.InOutCropEffectName;

        [Display(GroupName = nameof(Texts.InOutCropEffectGroupName), Name = nameof(Texts.InOutCropEffectInCropDirectionName), Description = nameof(Texts.InOutCropEffectInCropDirectionDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public CropDirection InCropDirection { get => inCropDirection; set => Set(ref inCropDirection, value); }
        CropDirection inCropDirection = CropDirection.Right;

        [Display(GroupName = nameof(Texts.InOutCropEffectGroupName), Name = nameof(Texts.InOutCropEffectOutCropDirectionName), Description = nameof(Texts.InOutCropEffectOutCropDirectionDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public CropDirection OutCropDirection { get => outCropDirection; set => Set(ref outCropDirection, value); }
        CropDirection outCropDirection = CropDirection.Right;

        [Display(GroupName = nameof(Texts.InOutCropEffectGroupName), Name = nameof(Texts.InOutCropEffectIsCenteringName), Description = nameof(Texts.InOutCropEffectIsCenteringDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Centering { get => centering; set => Set(ref centering, value); }
        bool centering = false;

        [Display(GroupName = nameof(Texts.InOutCropEffectGroupName), Name = nameof(Texts.InOutCropEffectEffectDurationName), Description = nameof(Texts.InOutCropEffectEffectDurationDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.InOutCropEffectSecUnit), 0, 0.5, ResourceType = typeof(Texts))]
        [DefaultValue(0.3)]
        [Range(0, 99999)]
        public double EffectDuration { get => effectDuration; set => Set(ref effectDuration, value); }
        double effectDuration = 0.3;

        [Display(GroupName = nameof(Texts.InOutCropEffectGroupName), Name = nameof(Texts.InOutCropEffectEasingTypeName), Description = nameof(Texts.InOutCropEffectEasingTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType{ get => easingType; set => Set(ref easingType, value); }
        EasingType easingType = EasingType.Expo;

        [Display(GroupName = nameof(Texts.InOutCropEffectGroupName), Name = nameof(Texts.InOutCropEffectEasingModeName), Description = nameof(Texts.InOutCropEffectEasingModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
        EasingMode easingMode = EasingMode.Out;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={effectDuration:F2}\r\n" +
                $"track1={(int)inCropDirection}\r\n" +
                $"track2={(int)outCropDirection}\r\n" +
                $"check0={(centering ? 1 : 0)}\r\n" +
                $"name=クリッピングを解除しながら登場退場@YMM4-未実装\r\n" +
                $"param=" +
                    $"local type=\"{easingType}\";" +
                    $"local mode=\"{easingMode}\";" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new InOutCropEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}