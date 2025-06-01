using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource
{
    internal class DistantLightSourceParameter : LightSourceParameterBase
    {
        [Display(Name = nameof(Texts.Azimuth), Description = nameof(Texts.Azimuth), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Azimuth { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Elevation), Description = nameof(Texts.Elevation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Elevation { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"track0={Azimuth.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Elevation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=平行光源（描画）@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }
        protected override IEnumerable<IAnimatable> GetAnimatables() => [Azimuth, Elevation];

        public class SharedData
        {
            public Animation Azimuth { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
            public Animation Elevation { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
            public SharedData()
            {

            }
            public SharedData(DistantLightSourceParameter parameter)
            {
                Azimuth.CopyFrom(parameter.Azimuth);
                Elevation.CopyFrom(parameter.Elevation);
            }
            public void CopyTo(DistantLightSourceParameter parameter)
            {
                parameter.Azimuth.CopyFrom(Azimuth);
                parameter.Elevation.CopyFrom(Elevation);
            }
        }
    }
}
