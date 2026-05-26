using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public class PuppetPin : Animatable
    {
        [JsonIgnore]
        public bool IsRestSelected { get => isRestSelected; set => Set(ref isRestSelected, value); }
        bool isRestSelected = false;

        [JsonIgnore]
        public bool IsOffsetSelected { get => isOffsetSelected; set => Set(ref isOffsetSelected, value); }
        bool isOffsetSelected = false;

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Name = nameof(Texts.PuppetPinEnabledName), Description = nameof(Texts.PuppetPinEnabledDesc), Order = 0, ResourceType = typeof(Texts))]
        [PuppetPinOffsetVisible]
        [ToggleSlider(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool IsEnabled { get => isEnabled; set => Set(ref isEnabled, value); }
        bool isEnabled = true;

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Name = nameof(Texts.PuppetPinRestXName), Description = nameof(Texts.PuppetPinRestXDesc), Order = 1, ResourceType = typeof(Texts))]
        [PuppetPinRestVisible]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation RestX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Name = nameof(Texts.PuppetPinRestYName), Description = nameof(Texts.PuppetPinRestYDesc), Order = 2, ResourceType = typeof(Texts))]
        [PuppetPinRestVisible]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation RestY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Name = nameof(Texts.PuppetPinOffsetXName), Description = nameof(Texts.PuppetPinOffsetXDesc), Order = 3, ResourceType = typeof(Texts))]
        [PuppetPinOffsetVisible]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation OffsetX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Name = nameof(Texts.PuppetPinOffsetYName), Description = nameof(Texts.PuppetPinOffsetYDesc), Order = 4, ResourceType = typeof(Texts))]
        [PuppetPinOffsetVisible]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation OffsetY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public static PuppetPin Create(double restX, double restY)
        {
            var pin = new PuppetPin { IsEnabled = true };
            pin.RestX.Values[0].Value = restX;
            pin.RestY.Values[0].Value = restY;
            return pin;
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [RestX, RestY, OffsetX, OffsetY];
    }
}
