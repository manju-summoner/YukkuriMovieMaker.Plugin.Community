using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting
{
    internal abstract class LightingParameterBase : SharedParameterBase
    {
        [Display(GroupName = nameof(Texts.Extrusion), Name = nameof(Texts.SurfaceScale), ResourceType = typeof(Texts), Order = 10)]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation SurfaceScale { get; } = new(100, 0, 10000);

        public LightingParameterBase() : base()
        {

        }
        public LightingParameterBase(SharedDataStore store) : base(store)
        {

        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [SurfaceScale];

        public abstract ILightingProcessor CreateLightingProcessor(IVideoEffect owner, IGraphicsDevicesAndContext devices);

        public virtual IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"track0={SurfaceScale.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=ライティング@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }
    }
}
