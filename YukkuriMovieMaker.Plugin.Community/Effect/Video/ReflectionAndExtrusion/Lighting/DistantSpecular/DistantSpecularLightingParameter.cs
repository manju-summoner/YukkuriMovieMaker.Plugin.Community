using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.DistantSpecular
{
    internal class DistantSpecularLightingParameter : LightingParameterBase
    {
        [Display(GroupName = nameof(Texts.Light), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public DistantLightSourceParameter LightSource { get; } = new();

        [Display(GroupName = nameof(Texts.Highlight), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public SpecularReflectionParameter Highlight { get; } = new();

        public DistantSpecularLightingParameter() : base()
        {

        }

        public DistantSpecularLightingParameter(SharedDataStore store) : base(store)
        {

        }

        public override ILightingProcessor CreateLightingProcessor(IVideoEffect owner, IGraphicsDevicesAndContext devices)
        {
            return new DistantSpecularLightingProcessor(devices, LightSource, Highlight, 0, SurfaceScale);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([LightSource, Highlight]);

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<DistantLightSourceParameter.SharedData>() is DistantLightSourceParameter.SharedData lightSourceData)
                lightSourceData.CopyTo(LightSource);
            if (store.Load<SpecularReflectionParameter.HightlightSharedData>() is SpecularReflectionParameter.HightlightSharedData highlightData)
                highlightData.CopyTo(Highlight);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new DistantLightSourceParameter.SharedData(LightSource));
            store.Save(new SpecularReflectionParameter.HightlightSharedData(Highlight));
        }

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            foreach(var filter in LightSource.CreateExoVideoFilters(isEnabled, keyFrameIndex, exoOutputDescription))
                yield return filter;
            foreach(var filter in Highlight.CreateExoVideoFilters(isEnabled, keyFrameIndex, exoOutputDescription))
                yield return filter;
        }
    }
}
