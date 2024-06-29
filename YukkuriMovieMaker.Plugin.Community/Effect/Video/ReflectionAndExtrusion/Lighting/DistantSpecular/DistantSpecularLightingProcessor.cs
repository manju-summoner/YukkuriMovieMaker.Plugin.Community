using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection;
using YukkuriMovieMaker.Plugin.Effects;
using D2D = Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.DistantSpecular
{
    internal class DistantSpecularLightingProcessor : ILightingProcessor
    {
        readonly DisposeCollector disposer = new();
        readonly DistantLightSourceParameter lightParameter;
        readonly SpecularReflectionParameter reflectionParameter;
        readonly float offset;
        readonly Animation surfaceHeightAnimation;

        public ID2D1Image Output { get; }
        public Project.Blend Blend => reflectionParameter.Blend;

        readonly D2D.Effects.DistantSpecular specular;
        readonly Flood flood;
        readonly AlphaMask alphaMask;

        bool isFirst = true;
        double azimuth, elevation, constant, exponent, surfaceScale;
        System.Windows.Media.Color color;

        public DistantSpecularLightingProcessor(
            IGraphicsDevicesAndContext devices,
            DistantLightSourceParameter lightParameter,
            SpecularReflectionParameter reflectionParameter,
            float offset, 
            Animation surfaceHeightAnimation)
        {
            this.lightParameter = lightParameter;
            this.reflectionParameter = reflectionParameter;
            this.offset = offset;
            this.surfaceHeightAnimation = surfaceHeightAnimation;

            specular = new(devices.DeviceContext)
            {
                ScaleMode = DistantSpecularScaleMode.HighQualityCubic,
                Color = new Vector3(1f, 1f, 1f)
            };
            disposer.Collect(specular);

            flood = new(devices.DeviceContext);
            disposer.Collect(flood);

            alphaMask = new(devices.DeviceContext);
            disposer.Collect(alphaMask);

            using(var image = flood.Output)
                alphaMask.SetInput(0, image, true);
            using(var image = specular.Output)
                alphaMask.SetInput(1, image, true);

            Output = alphaMask.Output;
            disposer.Collect(Output);
        }

        public void SetInput(ID2D1Image? input)
        {
            specular.SetInput(0, input, true);
        }

        public DrawDescription Update(EffectDescription desc)
        {
            var fps = desc.FPS;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;

            var azimuth = lightParameter.Azimuth.GetValue(frame, length, fps) + offset;
            var elevation = lightParameter.Elevation.GetValue(frame, length, fps);
            var constant = reflectionParameter.Constant.GetValue(frame, length, fps) / 100;
            var exponent = reflectionParameter.Exponent.GetValue(frame, length, fps);
            var color = reflectionParameter.Color;
            var surfaceScale = surfaceHeightAnimation.GetValue(frame, length, fps);
            azimuth = (azimuth % 360 + 360) % 360;
            elevation = (elevation % 360 + 360) % 360;

            if (isFirst || this.azimuth != azimuth)
                specular.Azimuth = (float)azimuth;
            if (isFirst || this.elevation != elevation)
                specular.Elevation = (float)elevation;
            if (isFirst || this.constant != constant)
                specular.SpecularConstant = (float)constant;
            if (isFirst || this.exponent != exponent)
                specular.SpecularExponent = (float)exponent;
            if (isFirst || this.surfaceScale != surfaceScale)
                specular.SurfaceScale = (float)surfaceScale;
            if (isFirst || this.color != color)
                flood.Color = color.ToVector4();

            isFirst = false;
            this.azimuth = azimuth;
            this.elevation = elevation;
            this.constant = constant;
            this.exponent = exponent;
            this.color = color;
            this.surfaceScale = surfaceScale;

            return desc.DrawDescription;
        }

        public void ClearInput()
        {
            specular.SetInput(0, null, true);
        }

        public void Dispose()
        {
            specular.SetInput(0, null, true);
            alphaMask.SetInput(0, null, true);
            alphaMask.SetInput(1, null, true);
            disposer.Dispose();
        }

    }
}