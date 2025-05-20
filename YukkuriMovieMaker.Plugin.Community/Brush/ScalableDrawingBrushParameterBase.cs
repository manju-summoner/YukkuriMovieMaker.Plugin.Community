using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Resources.Localization;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Brush
{
    internal abstract class ScalableDrawingBrushParameterBase : DrawingBrushParameterBase
    {
        [Display(Name = nameof(Texts.BrushParameterZoom), Description = nameof(Texts.BrushParameterZoom), Order = 250, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation Zoom { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        public override Matrix3x2 CreateBrushMatrix(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var zoom = (float)Zoom.GetValue(frame, length, fps) / 100f;

            return
                Matrix3x2.CreateScale(zoom, zoom)
                * base.CreateBrushMatrix(desc);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([Zoom]);
    }
}
