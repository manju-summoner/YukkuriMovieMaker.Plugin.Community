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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.PointDiffuse
{
    using AlphaMask = D2D.Effects.AlphaMask;
    internal class PointDiffuseLightingProcessor : ILightingProcessor
    {
        readonly DisposeCollector disposer = new();
        readonly IVideoEffect owner;
        readonly PointLightSourceParameter lightParameter;
        readonly DiffuseReflectionParameter reflectionParameter;
        readonly Vector2 scale;
        readonly Animation surfaceHeightAnimation;

        public ID2D1Image Output { get; }
        public Project.Blend Blend => reflectionParameter.Blend;

        readonly D2D.Effects.PointDiffuse diffuse;
        readonly DiffuseAlphaCustomEffect diffuseAlpha;
        readonly Flood flood;
        readonly AlphaMask alphaMask;

        bool isFirst = true;
        double x, y, z, constant, surfaceScale;
        System.Windows.Media.Color color;

        public PointDiffuseLightingProcessor(
            IVideoEffect owner,
            IGraphicsDevicesAndContext devices,
            PointLightSourceParameter lightParameter,
            DiffuseReflectionParameter reflectionParameter,
            Vector2 scale, 
            Animation surfaceHeightAnimation)
        {
            this.owner = owner;
            this.lightParameter = lightParameter;
            this.reflectionParameter = reflectionParameter;
            this.scale = scale;
            this.surfaceHeightAnimation = surfaceHeightAnimation;

            diffuse = new(devices.DeviceContext)
            {
                ScaleMode = PointDiffuseScaleMode.HighQualityCubic,
                Color = new Vector3(1f, 1f, 1f)
            };
            disposer.Collect(diffuse);

            diffuseAlpha = new(devices);
            disposer.Collect(diffuseAlpha);

            flood = new(devices.DeviceContext);
            disposer.Collect(flood);

            alphaMask = new(devices.DeviceContext);
            disposer.Collect(alphaMask);

            if (diffuseAlpha.IsEnabled)
            {
                using (var image = diffuse.Output)
                    diffuseAlpha.SetInput(0, image, true);
                using (var image = flood.Output)
                    alphaMask.SetInput(0, image, true);
                using (var image = diffuseAlpha.Output)
                    alphaMask.SetInput(1, image, true);
            }
            else
            {
                using(var image = flood.Output)
                    alphaMask.SetInput(0, image, true);
                using(var image = diffuse.Output)
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

            var x = lightParameter.X.GetValue(frame, length, fps);
            var y = lightParameter.Y.GetValue(frame, length, fps);
            var z = lightParameter.Z.GetValue(frame, length, fps);
            var constant = reflectionParameter.Constant.GetValue(frame, length, fps) / 100;
            var color = reflectionParameter.Color;
            var surfaceScale = surfaceHeightAnimation.GetValue(frame, length, fps);

            if (isFirst || this.x != x || this.y != y || this.z != z)
                diffuse.LightPosition = new Vector3((float)x * scale.X, (float)y * scale.Y, (float)z);
            if (isFirst || this.constant != constant)
                diffuse.DiffuseConstant = (float)constant;
            if (isFirst || this.surfaceScale != surfaceScale)
                diffuse.SurfaceScale = (float)surfaceScale;
            if (isFirst || this.color != color)
                flood.Color = color.ToVector4();

            var controller = lightParameter.CreateController(owner, desc);

            isFirst = false;
            this.x = x;
            this.y = y;
            this.z = z;
            this.constant = constant;
            this.color = color;
            this.surfaceScale = surfaceScale;

            return desc.DrawDescription with
            {
                Controllers = 
                [
                    .. desc.DrawDescription.Controllers,
                    controller,
                ],
            };
        }

        public void ClearInput()
        {
            diffuse.SetInput(0, null, true);
        }

        public void Dispose()
        {
            diffuse.SetInput(0, null, true);
            if(diffuseAlpha.IsEnabled)
                diffuseAlpha.SetInput(0, null, true);
            alphaMask.SetInput(0, null, true);
            alphaMask.SetInput(1, null, true);
            disposer.Dispose();
        }

    }
}