using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Resources.Localization;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Brush
{
    internal abstract class DrawingBrushParameterBase : BrushParameterBase
    {
        [Display(Name = nameof(Texts.BrushParameterX), Description = nameof(Texts.BrushParameterX), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.BrushParameterY), Description = nameof(Texts.BrushParameterY), Order = 200, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.BrushParameterAngle), Description = nameof(Texts.BrushParameterAngle), Order = 300, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.BrushParameterAspect), Description = nameof(Texts.BrushParameterAspect), Order = 400, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation Aspect { get; } = new Animation(0, -100, 100);

        [Display(Name = nameof(Texts.BrushParameterIsInverted), Description = nameof(Texts.BrushParameterIsInverted), Order = 500, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInverted { get => isInverted; set => Set(ref isInverted, value); }
        bool isInverted = false;

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Angle, Aspect];

        public virtual Matrix3x2 CreateBrushMatrix(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var x = (float)X.GetValue(frame, length, fps);
            var y = (float)Y.GetValue(frame, length, fps);
            var angle = (float)Angle.GetValue(frame, length, fps) / 180f * MathF.PI;
            var aspect = (float)Aspect.GetValue(frame, length, fps) / 100f;

            return
                Matrix3x2.CreateScale(1 - Math.Max(0, aspect), 1 + Math.Min(aspect, 0))
                * Matrix3x2.CreateScale(isInverted ? -1 : 1, 1)
                * Matrix3x2.CreateRotation(angle)
                * Matrix3x2.CreateTranslation(x, y);
        }
    }
}
