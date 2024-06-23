using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.BevelHeightmap
{
    internal class BevelHeightmapParameter : HeightmapParameterBase
    {
        [Display(Name = nameof(Texts.Thickness), Description = nameof(Texts.Thickness), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Thickness { get; } = new Animation(3, 0, 500);

        public BevelHeightmapParameter()
        {
        }

        public BevelHeightmapParameter(SharedDataStore store) : base(store)
        {
        }

        public override IVideoEffectProcessor CreateHeightmapSource(IGraphicsDevicesAndContext devices)
        {
            return new BevelHeightmapSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Thickness];

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<SharedData>() is SharedData data)
                data.CopyTo(this);
        }

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"track0={Thickness.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=ベベルハイトマップ@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }

        class SharedData
        {
            public Animation Thickness { get; } = new Animation(3, 0, 500);

            public SharedData()
            {
            }
            public SharedData(BevelHeightmapParameter parameter)
            {
                Thickness.CopyFrom(parameter.Thickness);
            }
            public void CopyTo(BevelHeightmapParameter parameter)
            {
                parameter.Thickness.CopyFrom(Thickness);
            }
        }
    }
}
