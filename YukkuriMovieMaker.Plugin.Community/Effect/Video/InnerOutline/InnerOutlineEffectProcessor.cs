using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InnerOutline
{
    using AlphaMask = Vortice.Direct2D1.Effects.AlphaMask;
    public class InnerOutlineEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly InnerOutlineEffect item;

        bool isFirst = true;
        double thickness, blur, opacity;
        RawRectF bounds;
        int width, height;

        readonly IBrushSource brushSource;
        ID2D1CommandList? brushCommandList;

        InnerOutlineCustomEffect? outlineEffect;
        ID2D1Image? outlineOutput;
        AffineTransform2D? centeringEffect;
        AlphaMask? alphaMaskEffect;
        GaussianBlur? blurEffect;
        Opacity? opacityEffect;
        ID2D1Image? opacityOutput;
        ID2D1Bitmap1? commandList;
        AffineTransform2D? centeringEffect2;
        AlphaMask? alphaMaskEffect2;

        public InnerOutlineEffectProcessor(IGraphicsDevicesAndContext devices, InnerOutlineEffect item) : base(devices)
        {
            this.devices = devices;
            this.item = item;
            brushSource = item.Brush.CreateBrush(devices);
            disposer.Collect(brushSource);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect
                || outlineEffect is null 
                || opacityEffect is null 
                || blurEffect is null
                || outlineEffect is null
                || centeringEffect is null
                || centeringEffect2 is null
                || opacityOutput is null
                || input is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var thickness = item.Thickness.GetValue(frame, length, fps);
            var blur = item.Blur.GetValue(frame, length, fps);
            var opacity = item.Opacity.GetValue(frame, length, fps);
            var isOutlineOnly = item.IsOutlineOnly;
            var blend = item.Blend;

            if (isFirst || this.thickness != thickness)
                outlineEffect.Thickness = (float)thickness;
            if (isFirst || this.opacity != opacity)
                opacityEffect.Value = (float)opacity / 100f;
            if (isFirst || this.blur != blur)
            {
                blurEffect.StandardDeviation = (float)blur;
                outlineEffect.Margin = (float)blur * 3;
            }

            var dc = devices.DeviceContext;
            //boundsはeffectにthicknessを設定したあとに計算する必要がある
            var bounds = dc.GetImageLocalBounds(outlineOutput);
            if (isFirst || !this.bounds.Equals(bounds))
            {
                centeringEffect.TransformMatrix = Matrix3x2.CreateTranslation(bounds.Left, bounds.Top);
                centeringEffect2.TransformMatrix = Matrix3x2.CreateTranslation(bounds.Left, bounds.Top);
            }

            var width = Math.Clamp((int)Math.Ceiling(bounds.Right - bounds.Left), 1, devices.DeviceContext.MaximumBitmapSize);
            var height = Math.Clamp((int)Math.Ceiling(bounds.Bottom - bounds.Top), 1, devices.DeviceContext.MaximumBitmapSize);
            var isBrushChanged = brushSource.Update(effectDescription);
            if (isFirst || isBrushChanged || !this.bounds.Equals(bounds) || this.width != width || this.height != height)
            {
                if (brushCommandList != null)
                    disposer.RemoveAndDispose(ref brushCommandList);
                brushCommandList = devices.DeviceContext.CreateCommandList();
                disposer.Collect(brushCommandList);

                dc.Target = brushCommandList;
                dc.BeginDraw();
                dc.Clear(null);

                dc.FillRectangle(new RawRectF(0, 0, width, height), brushSource.Brush);

                dc.EndDraw();
                dc.Target = null;
                brushCommandList.Close();

                centeringEffect.SetInput(0, brushCommandList, true);
            }

            if (isFirst || this.width != width || this.height != height)
            {
                if (commandList != null)
                    disposer.RemoveAndDispose(ref commandList);
                commandList = devices.DeviceContext.CreateEmptyBitmap(width, height);
                disposer.Collect(commandList);
            }
            var offset = new Vector2(-bounds.Left + (bounds.Right - bounds.Left - width) / 2f, -bounds.Top + (bounds.Bottom - bounds.Top - height) / 2f);

            //立ち絵等、大きな画像に対してエフェクトを適用するとなぜか上手く動作しないため、一度Bitmapに描画する
            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            if (!isOutlineOnly)
                dc.DrawImage(input, offset);
            if (blend.IsCompositionEffect())
                dc.DrawImage(opacityOutput, offset, compositeMode: blend.ToD2DCompositionMode());
            else
                dc.BlendImage(opacityOutput, blend.ToD2DBlendMode(), offset, null, InterpolationMode.HighQualityCubic);
            dc.EndDraw();
            dc.Target = null;

            centeringEffect2.SetInput(0, commandList, true);

            isFirst = false;
            this.thickness = thickness;
            this.blur = blur;
            this.opacity = opacity;
            this.bounds = bounds;
            this.width = width;
            this.height = height;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            outlineEffect = new InnerOutlineCustomEffect(devices);
            if (!outlineEffect.IsEnabled)
            {
                outlineEffect.Dispose();
                outlineEffect = null;
                return null;
            }
            disposer.Collect(outlineEffect);

            centeringEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(centeringEffect);

            alphaMaskEffect = new AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect);

            blurEffect = new GaussianBlur(devices.DeviceContext);
            disposer.Collect(blurEffect);

            opacityEffect = new Opacity(devices.DeviceContext);
            disposer.Collect(opacityEffect);

            centeringEffect2 = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(centeringEffect2);

            alphaMaskEffect2 = new AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect2);


            outlineOutput = outlineEffect.Output;
            disposer.Collect(outlineOutput);

            using (var output = centeringEffect.Output)
                alphaMaskEffect.SetInput(0, output, true);
            using (var output = outlineEffect.Output)
                alphaMaskEffect.SetInput(1, output, true);
            using (var output = alphaMaskEffect.Output)
                blurEffect.SetInput(0, output, true);
            using (var output = blurEffect.Output)
                opacityEffect.SetInput(0, output, true);
            opacityOutput = opacityEffect.Output;
            disposer.Collect(opacityOutput);

            using (var output = centeringEffect2.Output)
                alphaMaskEffect2.SetInput(0, output, true);
            var result = alphaMaskEffect2.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void setInput(ID2D1Image? input)
        {
            outlineEffect?.SetInput(0, input, true);
            alphaMaskEffect2?.SetInput(1, input, true);
        }

        protected override void ClearEffectChain()
        {
            outlineEffect?.SetInput(0, null, true);
            centeringEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(1, null, true);
            blurEffect?.SetInput(0, null, true);
            opacityEffect?.SetInput(0, null, true);
            centeringEffect2?.SetInput(0, null, true);
            alphaMaskEffect2?.SetInput(0, null, true);
            alphaMaskEffect2?.SetInput(1, null, true);
        }
    }
}