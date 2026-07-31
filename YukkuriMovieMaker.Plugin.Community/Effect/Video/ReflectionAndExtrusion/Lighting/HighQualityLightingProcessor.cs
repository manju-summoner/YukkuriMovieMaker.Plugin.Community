using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection;
using YukkuriMovieMaker.Plugin.Effects;
using D2DAlphaMask = Vortice.Direct2D1.Effects.AlphaMask;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting
{
    internal readonly record struct BevelSelfShadowSettings(float Strength, float Distance, float Bias, float Softness, OcclusionQuality Quality);

    internal sealed class HighQualityLightingProcessor : ILightingProcessor
    {
        readonly DisposeCollector disposer = new();
        readonly IVideoEffect? owner;
        readonly PointLightSourceParameter? pointLight;
        readonly DistantLightSourceParameter? distantLight;
        readonly ReflectionParameterBase reflection;
        readonly SpecularReflectionParameter? specularReflection;
        readonly Vector2 pointScale;
        readonly float azimuthOffset;
        readonly Animation surfaceScaleAnimation;
        readonly BevelLightingCustomEffect lighting;
        readonly Flood flood;
        readonly D2DAlphaMask alphaMask;

        bool isFirst = true;
        Vector4 currentLight;
        double currentConstant;
        double currentExponent;
        double currentSurfaceScale;
        System.Windows.Media.Color currentColor;

        public ID2D1Image Output { get; }
        public Project.Blend Blend => reflection.Blend;

        HighQualityLightingProcessor(
            BevelLightingCustomEffect lighting,
            IGraphicsDevicesAndContext devices,
            ReflectionParameterBase reflection,
            Animation surfaceScaleAnimation,
            IVideoEffect? owner,
            PointLightSourceParameter? pointLight,
            DistantLightSourceParameter? distantLight,
            Vector2 pointScale,
            float azimuthOffset)
        {
            this.lighting = lighting;
            this.reflection = reflection;
            specularReflection = reflection as SpecularReflectionParameter;
            this.surfaceScaleAnimation = surfaceScaleAnimation;
            this.owner = owner;
            this.pointLight = pointLight;
            this.distantLight = distantLight;
            this.pointScale = pointScale;
            this.azimuthOffset = azimuthOffset;

            disposer.Collect(lighting);
            lighting.LightMode = pointLight is null ? 0 : 1;
            lighting.ReflectionMode = specularReflection is null ? 0 : 1;

            flood = new Flood(devices.DeviceContext);
            disposer.Collect(flood);
            alphaMask = new D2DAlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMask);
            using (var image = flood.Output)
                alphaMask.SetInput(0, image, true);
            using (var image = lighting.Output)
                alphaMask.SetInput(1, image, true);

            Output = alphaMask.Output;
            disposer.Collect(Output);
        }

        public static HighQualityLightingProcessor? TryCreateDistant(
            IGraphicsDevicesAndContext devices,
            DistantLightSourceParameter light,
            ReflectionParameterBase reflection,
            float azimuthOffset,
            Animation surfaceScale)
        {
            var effect = new BevelLightingCustomEffect(devices);
            return effect.IsEnabled
                ? new HighQualityLightingProcessor(effect, devices, reflection, surfaceScale, null, null, light, Vector2.One, azimuthOffset)
                : DisposeAndReturnNull(effect);
        }

        public static HighQualityLightingProcessor? TryCreatePoint(
            IVideoEffect owner,
            IGraphicsDevicesAndContext devices,
            PointLightSourceParameter light,
            ReflectionParameterBase reflection,
            Vector2 scale,
            Animation surfaceScale)
        {
            var effect = new BevelLightingCustomEffect(devices);
            return effect.IsEnabled
                ? new HighQualityLightingProcessor(effect, devices, reflection, surfaceScale, owner, light, null, scale, 0)
                : DisposeAndReturnNull(effect);
        }

        static HighQualityLightingProcessor? DisposeAndReturnNull(BevelLightingCustomEffect effect)
        {
            effect.Dispose();
            return null;
        }

        public void SetInput(ID2D1Image? input) => lighting.SetInput(0, input, true);

        public void SetSelfShadowSettings(BevelSelfShadowSettings settings)
        {
            lighting.ShadowStrength = settings.Strength;
            lighting.ShadowDistance = settings.Distance;
            lighting.ShadowBias = settings.Bias;
            lighting.ShadowSoftness = settings.Softness;
            lighting.ShadowQuality = settings.Quality;
        }

        public DrawDescription Update(EffectDescription desc)
        {
            var fps = desc.FPS;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var light = GetLight(frame, length, fps);
            var constant = reflection.Constant.GetValue(frame, length, fps) / 100;
            var exponent = specularReflection?.Exponent.GetValue(frame, length, fps) ?? 1;
            var surfaceScale = surfaceScaleAnimation.GetValue(frame, length, fps);
            var color = reflection.Color;

            if (isFirst || currentLight != light)
                lighting.Light = light;
            if (isFirst || currentConstant != constant)
                lighting.ReflectionConstant = (float)constant;
            if (isFirst || currentExponent != exponent)
                lighting.Exponent = (float)exponent;
            if (isFirst || currentSurfaceScale != surfaceScale)
                lighting.SurfaceScale = (float)surfaceScale;
            if (isFirst || currentColor != color)
                flood.Color = color.ToVector4();

            isFirst = false;
            currentLight = light;
            currentConstant = constant;
            currentExponent = exponent;
            currentSurfaceScale = surfaceScale;
            currentColor = color;

            if (pointLight is null || owner is null)
                return desc.DrawDescription;

            return desc.DrawDescription with
            {
                Controllers =
                [
                    ..desc.DrawDescription.Controllers,
                    pointLight.CreateController(owner, desc),
                ],
            };
        }

        Vector4 GetLight(int frame, int length, int fps)
        {
            if (pointLight is not null)
            {
                return new Vector4(
                    (float)pointLight.X.GetValue(frame, length, fps) * pointScale.X,
                    (float)pointLight.Y.GetValue(frame, length, fps) * pointScale.Y,
                    (float)pointLight.Z.GetValue(frame, length, fps),
                    0);
            }

            var azimuth = ((distantLight?.Azimuth.GetValue(frame, length, fps) ?? 0) + azimuthOffset) * Math.PI / 180;
            var elevation = (distantLight?.Elevation.GetValue(frame, length, fps) ?? 0) * Math.PI / 180;
            var cosElevation = Math.Cos(elevation);
            return new Vector4(
                (float)(cosElevation * Math.Cos(azimuth)),
                (float)(cosElevation * Math.Sin(azimuth)),
                (float)Math.Sin(elevation),
                0);
        }

        public void ClearInput() => lighting.SetInput(0, null, true);

        public void Dispose()
        {
            lighting.SetInput(0, null, true);
            alphaMask.SetInput(0, null, true);
            alphaMask.SetInput(1, null, true);
            disposer.Dispose();
        }
    }
}
