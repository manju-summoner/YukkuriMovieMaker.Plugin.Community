using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.PointDiffuse
{
    internal class PointDiffuseLightingParameter : LightingParameterBase
    {
        [Display(GroupName = nameof(Texts.Light), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public PointLightSourceParameter LightSource { get; } = new();

        [Display(GroupName = nameof(Texts.Highlight), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public DiffuseReflectionParameter Highlight { get; } = new();

        public PointDiffuseLightingParameter() : base()
        {

        }

        public PointDiffuseLightingParameter(SharedDataStore store) : base(store)
        {

        }

        public override ILightingProcessor CreateLightingProcessor(IVideoEffect owner, IGraphicsDevicesAndContext devices)
        {
            return new PointDiffuseLightingProcessor(owner, devices, LightSource, Highlight, new System.Numerics.Vector2(1,1), SurfaceScale);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([LightSource, Highlight]);

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<PointLightSourceParameter.SharedData>() is PointLightSourceParameter.SharedData lightSourceData)
                lightSourceData.CopyTo(LightSource);
            if (store.Load<DiffuseReflectionParameter.HightlightSharedData>() is DiffuseReflectionParameter.HightlightSharedData highlightData)
                highlightData.CopyTo(Highlight);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new PointLightSourceParameter.SharedData(LightSource));
            store.Save(new DiffuseReflectionParameter.HightlightSharedData(Highlight));
        }
    }
}
