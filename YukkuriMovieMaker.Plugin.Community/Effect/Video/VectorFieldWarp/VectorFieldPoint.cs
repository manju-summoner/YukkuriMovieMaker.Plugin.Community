using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    public sealed class VectorFieldPoint : Animatable
    {
        public const float StrengthLimit = 4096f;
        public const float RadiusLimit = 4096f;

        [JsonIgnore]
        public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }
        bool isSelected;

        [Display(GroupName = nameof(Texts.VectorFieldPointGroupName), Name = nameof(Texts.VectorFieldPointEnabledName), Description = nameof(Texts.VectorFieldPointEnabledDesc), Order = 0, ResourceType = typeof(Texts))]
        [ToggleSlider(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool IsEnabled { get => isEnabled; set => Set(ref isEnabled, value); }
        bool isEnabled = true;

        [Display(GroupName = nameof(Texts.VectorFieldPointGroupName), Name = nameof(Texts.VectorFieldPointXName), Description = nameof(Texts.VectorFieldPointXDesc), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.VectorFieldPointGroupName), Name = nameof(Texts.VectorFieldPointYName), Description = nameof(Texts.VectorFieldPointYDesc), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.VectorFieldPointGroupName), Name = nameof(Texts.VectorFieldPointRadialStrengthName), Description = nameof(Texts.VectorFieldPointRadialStrengthDesc), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -200, 200)]
        public Animation RadialStrength { get; } = new(0, -StrengthLimit, StrengthLimit);

        [Display(GroupName = nameof(Texts.VectorFieldPointGroupName), Name = nameof(Texts.VectorFieldPointVortexStrengthName), Description = nameof(Texts.VectorFieldPointVortexStrengthDesc), Order = 4, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -200, 200)]
        public Animation VortexStrength { get; } = new(120, -StrengthLimit, StrengthLimit);

        [Display(GroupName = nameof(Texts.VectorFieldPointGroupName), Name = nameof(Texts.VectorFieldPointRadiusName), Description = nameof(Texts.VectorFieldPointRadiusDesc), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 1, 500)]
        public Animation Radius { get; } = new(200, 1, RadiusLimit);

        public static VectorFieldPoint Create(double x, double y, double radialStrength = 0, double vortexStrength = 120, double radius = 200)
        {
            var point = new VectorFieldPoint();
            point.X.Values[0].Value = x;
            point.Y.Values[0].Value = y;
            point.RadialStrength.Values[0].Value = radialStrength;
            point.VortexStrength.Values[0].Value = vortexStrength;
            point.Radius.Values[0].Value = radius;
            return point;
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, RadialStrength, VortexStrength, Radius];
    }
}
