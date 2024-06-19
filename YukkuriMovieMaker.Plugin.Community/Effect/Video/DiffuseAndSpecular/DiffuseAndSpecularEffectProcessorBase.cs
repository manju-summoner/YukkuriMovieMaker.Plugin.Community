using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.FileSource;
using Blend = Vortice.Direct2D1.Effects.Blend;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    internal abstract class DiffuseAndSpecularEffectProcessorBase<TDiffuse,TSpecular>(IGraphicsDevicesAndContext devices, DiffuseAndSpecularEffectBase diffuseAndSpecularEffect) : VideoEffectProcessorBase(devices) where TDiffuse : IDiffuseEffectWrapper where TSpecular : ISpecularEffectWrapper
    {
        readonly IGraphicsDevicesAndContext devices = devices;
        Flood? flat;
        IVideoFileSource? videoSource;
        IImageFileSource? imageSource;
        LuminanceToAlpha? luminanceToAlpha;
        InvertAlphaCustomEffect? invertAlpha;
        AffineTransform2D? hightTransform;

        protected TSpecular? specular;
        GaussianBlur? specularBlur;
        Composite? specularComposite;
        Blend? specularBlendEffect;

        protected TDiffuse? diffuse;
        DiffuseAlphaCustomEffect? diffuseAlpha;
        GaussianBlur? diffuseBlur;
        Composite? diffuseComposite;
        Blend? diffuseBlendEffect;

        AlphaMask? alphaMask;
        AffineTransform2D? wrap;

        /*
         image/video or flat -> luminanceToAlpha -> invertAlpha -> transform -> hightOutput

                                                          hightOutput -> specular -> specularBlur -+
         hightOutput -> diffuse -> diffuseAlpha -> diffuseBlur -> composite or blend -> composite or blend -> alphaMask -> wrap -> output
                                                                     input -+                              input-+
         */

        protected bool isFirst = true;
        double
            specularConstant, specularExponent,
            diffuseConstant,
            surfaceScale, blur;
        Color specularColor, diffuseColor;
        Project.Blend specularBlend, diffuseBlend;
        string? filePath;
        double heightmapWidth, heightmapHeight;
        Matrix3x2 heightmapMatrix;
        bool isInvert;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (flat is null || luminanceToAlpha is null || invertAlpha is null || hightTransform is null
                || specular is null || specularComposite is null || specularBlur is null || specularBlendEffect is null
                || diffuse is null || diffuseAlpha is null || diffuseBlur is null || diffuseComposite is null || diffuseBlendEffect is null
                || alphaMask is null || wrap is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var specularConstant = diffuseAndSpecularEffect.SpecularConstant.GetValue(frame, length, fps) / 100;
            var specularExponent = diffuseAndSpecularEffect.SpecularExponent.GetValue(frame, length, fps);
            var specularColor = diffuseAndSpecularEffect.SpecularColor;
            var specularBlend = diffuseAndSpecularEffect.SpecularBlend;

            var diffuseConstant = diffuseAndSpecularEffect.DiffuseConstant.GetValue(frame, length, fps) / 100;
            var diffuseColor = diffuseAndSpecularEffect.DiffuseColor;
            var diffuseBlend = diffuseAndSpecularEffect.DiffuseBlend;

            var filePath = diffuseAndSpecularEffect.FilePath;
            var surfaceScale = diffuseAndSpecularEffect.SurfaceScale.GetValue(frame, length, fps);
            var zoom = diffuseAndSpecularEffect.Zoom.GetValue(frame, length, fps) / 100;
            var blur = diffuseAndSpecularEffect.Blur.GetValue(frame, length, fps) / 3;
            var isInvert = diffuseAndSpecularEffect.IsInvert;

            if (isFirst || this.specularConstant != specularConstant)
                specular.SpecularConstant = (float)specularConstant;
            if (isFirst || this.specularExponent != specularExponent)
                specular.SpecularExponent = (float)specularExponent;
            if (isFirst || this.specularColor != specularColor)
                specular.Color = specularColor.ToVector3();

            if (isFirst || this.diffuseConstant != diffuseConstant)
                diffuse.DiffuseConstant = (float)diffuseConstant;
            if (isFirst || this.diffuseColor != diffuseColor)
                diffuse.Color = diffuseColor.ToVector3();

            if (isFirst || this.blur != blur)
            {
                diffuseBlur.StandardDeviation = (float)blur;
                specularBlur.StandardDeviation = (float)blur;
            }

            if (isFirst || this.specularBlend != specularBlend || this.diffuseBlend != diffuseBlend)
            {
                if (diffuseBlend.IsCompositionEffect())
                {
                    diffuseComposite.Mode = diffuseBlend.ToD2DCompositionMode();
                    if (specularBlend.IsCompositionEffect())
                    {
                        using (var image = diffuseComposite.Output)
                            specularComposite.SetInput(0, image, true);
                        using (var image = specularComposite.Output)
                            alphaMask.SetInput(0, image, true);

                        specularComposite.Mode = specularBlend.ToD2DCompositionMode();
                    }
                    else
                    {
                        using (var image = diffuseComposite.Output)
                            specularBlendEffect.SetInput(0, image, true);
                        using (var image = specularBlendEffect.Output)
                            alphaMask.SetInput(0, image, true);

                        specularBlendEffect.Mode = specularBlend.ToD2DBlendMode();
                    }
                }
                else
                {
                    diffuseBlendEffect.Mode = diffuseBlend.ToD2DBlendMode();
                    if (specularBlend.IsCompositionEffect())
                    {
                        using (var image = diffuseBlendEffect.Output)
                            specularComposite.SetInput(0, image, true);
                        using (var image = specularComposite.Output)
                            alphaMask.SetInput(0, image, true);

                        specularComposite.Mode = specularBlend.ToD2DCompositionMode();
                    }
                    else
                    {
                        using (var image = diffuseBlendEffect.Output)
                            specularBlendEffect.SetInput(0, image, true);
                        using (var image = specularBlendEffect.Output)
                            alphaMask.SetInput(0, image, true);

                        specularBlendEffect.Mode = specularBlend.ToD2DBlendMode();
                    }
                }
            }

            if (isFirst || this.surfaceScale != surfaceScale)
            {
                specular.SurfaceScale = (float)surfaceScale;
                diffuse.SurfaceScale = (float)surfaceScale;
            }

            if (isFirst || this.filePath != filePath)
            {
                if (videoSource is not null)
                    disposer.RemoveAndDispose(ref videoSource);
                if (imageSource is not null)
                    disposer.RemoveAndDispose(ref imageSource);

                if (!string.IsNullOrEmpty(filePath))
                {
                    videoSource = VideoFileSourceFactory.Create(devices, filePath);
                    if (videoSource is not null)
                    {
                        disposer.Collect(videoSource);
                        luminanceToAlpha.SetInput(0, videoSource.Output, true);
                        heightmapWidth = 0;
                        heightmapHeight = 0;
                    }
                    else
                    {
                        imageSource = ImageFileSourceFactory.Create(devices, filePath);
                        if (imageSource is not null)
                        {
                            disposer.Collect(imageSource);
                            luminanceToAlpha.SetInput(0, imageSource.Output, true);
                            heightmapWidth = imageSource.Output.PixelSize.Width;
                            heightmapHeight = imageSource.Output.PixelSize.Height;
                        }
                        else
                        {
                            using var image = flat.Output;
                            luminanceToAlpha.SetInput(0, image, true);
                            heightmapWidth = 0;
                            heightmapHeight = 0;
                        }
                    }
                }
                else
                {
                    using var image = flat.Output;
                    luminanceToAlpha.SetInput(0, image, true);
                    heightmapWidth = 0;
                    heightmapHeight = 0;
                }

            }

            var heightmapMatrix =
                Matrix3x2.CreateTranslation((float)-heightmapWidth / 2f, (float)-heightmapHeight / 2f)
                * Matrix3x2.CreateScale((float)zoom);
            if (isFirst || this.heightmapMatrix != heightmapMatrix)
                hightTransform.TransformMatrix = heightmapMatrix;

            if(isFirst || this.isInvert != isInvert)
                invertAlpha.Invert = isInvert ? 1 : 0;

            videoSource?.Update(effectDescription.ItemPosition.Time);


            isFirst = false;

            this.specularConstant = specularConstant;
            this.specularExponent = specularExponent;
            this.specularColor = specularColor;
            this.specularBlend = specularBlend;

            this.diffuseConstant = diffuseConstant;
            this.diffuseColor = diffuseColor;
            this.diffuseBlend = diffuseBlend;

            this.filePath = filePath;
            this.surfaceScale = surfaceScale;
            this.blur = blur;
            this.heightmapMatrix = heightmapMatrix;
            this.isInvert = isInvert;

            return effectDescription.DrawDescription;
        }

        protected override void ClearEffectChain()
        {
            luminanceToAlpha?.SetInput(0, null, true);
            invertAlpha?.SetInput(0, null, true);
            hightTransform?.SetInput(0, null, true);

            specular?.SetInput(0, null, true);
            specularBlur?.SetInput(0, null, true);
            specularComposite?.SetInput(0, null, true);
            specularComposite?.SetInput(1, null, true);
            specularBlendEffect?.SetInput(0, null, true);
            specularBlendEffect?.SetInput(1, null, true);

            diffuse?.SetInput(0, null, true);
            diffuseAlpha?.SetInput(0, null, true);
            diffuseBlur?.SetInput(0, null, true);
            diffuseComposite?.SetInput(0, null, true);
            diffuseComposite?.SetInput(1, null, true);
            diffuseBlendEffect?.SetInput(0, null, true);
            diffuseBlendEffect?.SetInput(1, null, true);

            alphaMask?.SetInput(0, null, true);
            alphaMask?.SetInput(1, null, true);
            wrap?.SetInput(0, null, true);
        }

        protected abstract TSpecular CreateSpecularEffect(IGraphicsDevicesAndContext devices);
        protected abstract TDiffuse CreateDiffuseEffect(IGraphicsDevicesAndContext devices);

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            diffuseAlpha = new DiffuseAlphaCustomEffect(devices);
            invertAlpha = new(devices);
            if (!diffuseAlpha.IsEnabled || !invertAlpha.IsEnabled)
            {
                diffuseAlpha.Dispose();
                invertAlpha.Dispose();
                diffuseAlpha = null;
                invertAlpha = null;
                return null;
            }
            disposer.Collect(diffuseAlpha);
            disposer.Collect(invertAlpha);

            flat = new(devices.DeviceContext) { Color = new Vector4(1f, 1f, 1f, 1f) };
            disposer.Collect(flat);

            luminanceToAlpha = new(devices.DeviceContext);
            disposer.Collect(luminanceToAlpha);

            hightTransform = new(devices.DeviceContext);
            disposer.Collect(hightTransform);

            specular = CreateSpecularEffect(devices);
            disposer.Collect(specular);

            specularBlur = new(devices.DeviceContext);
            disposer.Collect(specularBlur);

            specularComposite = new(devices.DeviceContext);
            disposer.Collect(specularComposite);

            specularBlendEffect = new(devices.DeviceContext);
            disposer.Collect(specularBlendEffect);

            diffuse = CreateDiffuseEffect(devices);
            disposer.Collect(diffuse);

            diffuseBlur = new(devices.DeviceContext);
            disposer.Collect(diffuseBlur);

            diffuseComposite = new(devices.DeviceContext);
            disposer.Collect(diffuseComposite);

            diffuseBlendEffect = new(devices.DeviceContext);
            disposer.Collect(diffuseBlendEffect);

            alphaMask = new(devices.DeviceContext);
            disposer.Collect(alphaMask);

            wrap = new(devices.DeviceContext);
            disposer.Collect(wrap);

            using (var image = luminanceToAlpha.Output)
                invertAlpha.SetInput(0, image, true);
            using (var image = invertAlpha.Output)
                hightTransform.SetInput(0, image, true);

            using (var image = hightTransform.Output)
            {
                specular.SetInput(0, image, true);
                diffuse.SetInput(0, image, true);
            }

            using (var image = specular.Output)
                specularBlur.SetInput(0, image, true);
            using (var image = specularBlur.Output)
            {
                specularComposite.SetInput(1, image, true);
                specularBlendEffect.SetInput(1, image, true);
            }

            using (var image = diffuse.Output)
                diffuseAlpha.SetInput(0, image, true);
            using(var image = diffuseAlpha.Output)
                diffuseBlur.SetInput(0, image, true);
            using (var image = diffuseBlur.Output)
            {
                diffuseComposite.SetInput(1, image, true);
                diffuseBlendEffect.SetInput(1, image, true);
            }

            using (var image = alphaMask.Output)
                wrap.SetInput(0, image, true);

            var output = wrap.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            alphaMask?.SetInput(1, input, true);
            diffuseComposite?.SetInput(0, input, true);
            diffuseBlendEffect?.SetInput(0, input, true);
        }
    }
}