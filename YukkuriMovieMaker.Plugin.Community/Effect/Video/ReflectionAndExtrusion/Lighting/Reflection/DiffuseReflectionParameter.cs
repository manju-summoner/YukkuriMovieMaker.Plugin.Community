using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection
{
    internal class DiffuseReflectionParameter : ReflectionParameterBase
    {
        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"track0={Constant.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=拡散反射@YMM4-未実装\r\n" +
                $"param=" +
                    $"local color={Color.R:X2}{Color.G:X2}{Color.B:X2};" +
                    $"local blend={(int)Blend};" +
                    $"\r\n";
        }

        public class SharedData : ReflectionParameterBaseSharedData
        {

            public SharedData() : base()
            {
            }
            public SharedData(DiffuseReflectionParameter parameter) : base(parameter)
            {
            }
            public void CopyTo(DiffuseReflectionParameter parameter)
            {
                base.CopyTo(parameter);
            }
        }
        public class HightlightSharedData : SharedData
        {
            public HightlightSharedData() : base()
            {
            }
            public HightlightSharedData(DiffuseReflectionParameter parameter) : base(parameter)
            {
            }
        }
        public class ShadowSharedData : SharedData
        {
            public ShadowSharedData() : base()
            {
            }
            public ShadowSharedData(DiffuseReflectionParameter parameter) : base(parameter)
            {
            }
        }
    }
}
