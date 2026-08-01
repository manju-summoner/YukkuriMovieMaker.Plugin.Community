using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.PointSpecular
{
    internal class PointSpecularLightingParameter : LightingParameterBase
    {
        [Display(GroupName = nameof(Texts.Light), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public PointLightSourceParameter LightSource { get; } = new();

        [Display(GroupName = nameof(Texts.Highlight), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public SpecularReflectionParameter Highlight { get; } = new();

        public PointSpecularLightingParameter() : base()
        {

        }

        public PointSpecularLightingParameter(SharedDataStore store) : base(store)
        {

        }

        public override ILightingProcessor CreateLightingProcessor(IVideoEffect owner, IGraphicsDevicesAndContext devices)
        {
            return new PointSpecularLightingProcessor(owner, devices, LightSource, Highlight, new(1,1), SurfaceScale);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([LightSource, Highlight]);

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<PointLightSourceParameter.SharedData>() is PointLightSourceParameter.SharedData lightSourceData)
                lightSourceData.CopyTo(LightSource);
            if (store.Load<SpecularReflectionParameter.HightlightSharedData>() is SpecularReflectionParameter.HightlightSharedData highlightData)
                highlightData.CopyTo(Highlight);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new PointLightSourceParameter.SharedData(LightSource));
            store.Save(new SpecularReflectionParameter.HightlightSharedData(Highlight));
        }
    }
}
