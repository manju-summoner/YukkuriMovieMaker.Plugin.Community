using System.Numerics;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size
{
    internal abstract class SizeParameterBase : SharedParameterBase
    {
        public abstract string Label { get; }

        public SizeParameterBase()
        {
        }

        public SizeParameterBase(SharedDataStore? store = null) : base(store)
        {
        }

        public abstract Vector2 GetZoom(float sourceWidth, float sourceHeight, EffectDescription effectDescription);

        public abstract IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription, bool dot);
    }
}
