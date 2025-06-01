using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource
{
    internal class PointLightSourceParameter : LightSourceParameterBase
    {
        [Display(Name = nameof(Texts.XName), Description = nameof(Texts.XDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.YName), Description = nameof(Texts.YDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.ZName), Description = nameof(Texts.ZDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Z { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Z];

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"track0={X.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Y.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={Z.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=点光源@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }

        public VideoEffectController CreateController(IVideoEffect owner, EffectDescription desc)
        {
            var fps = desc.FPS;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;

            var x = X.GetValue(frame, length, fps);
            var y = Y.GetValue(frame, length, fps);
            var z = Z.GetValue(frame, length, fps);

            return new VideoEffectController(
                owner,
                [
                    new ControllerPoint(
                        new System.Numerics.Vector3(
                            (float)x,
                            (float)y,
                            (float)z),
                        arg=>
                        {
                            X.AddToEachValues(arg.Delta.X);
                            Y.AddToEachValues(arg.Delta.Y);
                            Z.AddToEachValues(arg.Delta.Z);
                        })
                    {
                        OnMouseWheel = arg=>
                        {
                            Z.AddToEachValues(arg.Delta);
                        }
                    }
                ]);
        }

        public class SharedData
        {
            public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
            public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
            public Animation Z { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

            public SharedData()
            {

            }
            public SharedData(PointLightSourceParameter parameter)
            {
                X.CopyFrom(parameter.X);
                Y.CopyFrom(parameter.Y);
                Z.CopyFrom(parameter.Z);
            }
            public void CopyTo(PointLightSourceParameter parameter)
            {
                parameter.X.CopyFrom(X);
                parameter.Y.CopyFrom(Y);
                parameter.Z.CopyFrom(Z);
            }
        }
    }
}
