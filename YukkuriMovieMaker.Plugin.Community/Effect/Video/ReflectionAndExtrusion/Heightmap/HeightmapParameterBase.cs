using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap
{
    internal abstract class HeightmapParameterBase : SharedParameterBase
    {
        public HeightmapParameterBase()
        {
        }

        public HeightmapParameterBase(SharedDataStore? store = null) : base(store)
        {
        }

        public abstract IVideoEffectProcessor CreateHeightmapSource(IGraphicsDevicesAndContext devices);

        public abstract IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription);
    }
}
