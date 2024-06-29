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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.DistantDiffuse
{
    internal class DistantDiffuseLightingProcessor : ILightingProcessor
    {
        readonly DisposeCollector disposer = new();
        readonly DistantLightSourceParameter lightParameter;
        readonly DiffuseReflectionParameter reflectionParameter;
        readonly float offset;
        readonly Animation surfaceHeightAnimation;

        public ID2D1Image Output { get; }
        public Project.Blend Blend => reflectionParameter.Blend;

        readonly D2D.Effects.DistantDiffuse diffuse;
        readonly DiffuseAlphaCustomEffect diffuseAlpha;
        readonly Flood flood;
        readonly AlphaMask alphaMask;

        bool isFirst = true;
        double azimuth, elevation, constant, surfaceScale;
        System.Windows.Media.Color color;

        public DistantDiffuseLightingProcessor(
            IGraphicsDevicesAndContext devices,
            DistantLightSourceParameter lightParameter,
            DiffuseReflectionParameter reflectionParameter,
            float offset, 
            Animation surfaceHeightAnimation)
        {
            this.lightParameter = lightParameter;
            this.reflectionParameter = reflectionParameter;
            this.offset = offset;
            this.surfaceHeightAnimation = surfaceHeightAnimation;

            diffuse = new(devices.DeviceContext) 
            { 
                ScaleMode = DistantDiffuseScaleMode.HighQualityCubic, 
                Color = new Vector3(1f,1f,1f)  
            };
            disposer.Collect(diffuse);

            diffuseAlpha = new(devices);
            disposer.Collect(diffuseAlpha);

            flood = new(devices.DeviceContext);
            disposer.Collect(flood);

            alphaMask = new(devices.DeviceContext);
            disposer.Collect(alphaMask);

            using (var image = flood.Output)
                alphaMask.SetInput(0, image, true);
            if (diffuseAlpha.IsEnabled)
            {
                using (var image = diffuse.Output)
                    diffuseAlpha.SetInput(0, image, true);
                using (var image = diffuseAlpha.Output)
                    alphaMask.SetInput(1, image, true);
            }
            else
            {
                using var image = diffuse.Output;
                alphaMask.SetInput(1, image, true);
            }
            Output = alphaMask.Output;
            disposer.Collect(Output);
        }

        public void SetInput(ID2D1Image? input)
        {
            diffuse.SetInput(0, input, true);
        }

        public DrawDescription Update(EffectDescription desc)
        {
            var fps = desc.FPS;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;

            var azimuth = (float)lightParameter.Azimuth.GetValue(frame, length, fps) + offset;
            var elevation = (float)lightParameter.Elevation.GetValue(frame, length, fps);
            var constant = reflectionParameter.Constant.GetValue(frame, length, fps) / 100;
            var color = reflectionParameter.Color;
            var surfaceScale = surfaceHeightAnimation.GetValue(frame, length, fps);
            azimuth = (azimuth % 360 + 360) % 360;
            elevation = (elevation % 360 + 360) % 360;

            if (isFirst || this.azimuth != azimuth)
                diffuse.Azimuth = azimuth;
            if (isFirst || this.elevation != elevation)
                diffuse.Elevation = elevation;
            if (isFirst || this.constant != constant)
                diffuse.DiffuseConstant = (float)constant;
            if (isFirst || this.surfaceScale != surfaceScale)
                diffuse.SurfaceScale = (float)surfaceScale;
            if (isFirst || this.color != color)
                flood.Color = color.ToVector4();

            isFirst = false;
            this.azimuth = azimuth;
            this.elevation = elevation;
            this.constant = constant;
            this.color = color;
            this.surfaceScale = surfaceScale;

            return desc.DrawDescription;
        }

        public void ClearInput()
        {
            diffuse.SetInput(0, null, true);
        }

        public void Dispose()
        {
            diffuse.SetInput(0, null, true);
            if (diffuseAlpha.IsEnabled)
                diffuseAlpha.SetInput(0, null, true);
            alphaMask.SetInput(0, null, true);
            alphaMask.SetInput(1, null, true);

            disposer.Dispose();
        }

    }
}