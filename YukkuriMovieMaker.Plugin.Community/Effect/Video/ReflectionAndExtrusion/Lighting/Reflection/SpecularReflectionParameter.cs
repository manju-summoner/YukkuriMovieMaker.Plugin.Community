using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection
{
    internal class SpecularReflectionParameter : ReflectionParameterBase
    {
        [Display(Name = nameof(Texts.ExponentName), Description = nameof(Texts.ExponentDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 1, 16)]
        public Animation Exponent { get; } = new Animation(1, 1, 128);

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"track0={Constant.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Exponent.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=鏡面反射@YMM4-未実装\r\n" +
                $"param=" +
                    $"local color={Color.R:X2}{Color.G:X2}{Color.B:X2};" +
                    $"local blend={(int)Blend};" +
                    $"\r\n";
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([Exponent]);

        public class SharedData : ReflectionParameterBaseSharedData
        {
            public Animation Exponent { get; } = new Animation(1, 1, 128);
            public SharedData() : base()
            {
            }
            public SharedData(SpecularReflectionParameter parameter) : base(parameter)
            {
                Exponent.CopyFrom(parameter.Exponent);
            }
            public void CopyTo(SpecularReflectionParameter parameter)
            {
                base.CopyTo(parameter);
                parameter.Exponent.CopyFrom(Exponent);
            }
        }

        public class HightlightSharedData : SharedData
        {
            public HightlightSharedData() : base()
            {

            }
            public HightlightSharedData(SpecularReflectionParameter parameter) : base(parameter)
            {

            }
        }

        public class ShadowSharedData : SharedData
        {
            public ShadowSharedData() : base()
            {

            }
            public ShadowSharedData(SpecularReflectionParameter parameter) : base(parameter)
            {

            }
        }
    }
}
