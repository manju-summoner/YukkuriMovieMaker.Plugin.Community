using System.Drawing;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using Size = System.Drawing.Size;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AfterImage
{
    internal class AfterImageEffectProcessor(IGraphicsDevicesAndContext devices, AfterImageEffect effect) : VideoEffectProcessorBase(devices)
    {
        readonly IGraphicsDevicesAndContext devices = devices;

        AffineTransform2D? zoomEffect;
        Crop? cropEffect;
        Transform3D? renderEffect;
        ID2D1Image? renderOutput;
        AffineTransform2D? centeringEffect;
        ColorMatrix? opacityEffect;
        Flood? floodEffect;
        Composite? compositeEffect;
        AfterImageCustomEffect? afterImageEffect;
        ID2D1Bitmap? feedbackBitmap;
        ID2D1Bitmap? feedbackTmpBitmap;
        AffineTransform2D? outputEffect;

        bool isFirst = true;
        int frame;
        Size screenSize;
        Vector3 draw, rotation;
        Vector2 zoom, centerPoint;
        Matrix4x4 camera;
        double opacity;
        bool invert;
        double strength;
        AfterImagePosition mode;

        public override DrawDescription Update(EffectDescription desc)
        {
            if (IsPassThroughEffect
                || zoomEffect is null
                || cropEffect is null
                || renderEffect is null
                || renderOutput is null
                || centeringEffect is null
                || opacityEffect is null
                || floodEffect is null
                || compositeEffect is null
                || afterImageEffect is null
                || outputEffect is null 
                || input is null)
                return desc.DrawDescription;

            var dc = devices.DeviceContext;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            
            var screenSize = desc.ScreenSize;

            var dd = desc.DrawDescription;
            var draw = dd.Draw;
            var centerPoint = dd.CenterPoint;
            var zoom = dd.Zoom;
            var rotation = dd.Rotation;
            var camera = dd.Camera;
            var opacity = dd.Opacity;
            var invert = dd.Invert;

            var strength = effect.Strength.GetValue(frame, length, fps) / 100;
            var mode = effect.Mode;

            if (!isFirst
                && feedbackBitmap != null
                && feedbackTmpBitmap != null
                && this.frame == frame
                && this.screenSize == screenSize
                && this.draw == draw
                && this.centerPoint == centerPoint
                && this.zoom == zoom
                && this.rotation == rotation
                && this.camera == camera
                && this.opacity == opacity
                && this.invert == invert
                && this.strength == strength
                && this.mode == mode)
                return CreateResultDrawDescription(desc);

            if (isFirst || frame == 0 || frame <= this.frame || this.screenSize != screenSize || feedbackBitmap is null || feedbackTmpBitmap is null)
            {
                this.frame = frame;

                disposer.RemoveAndDispose(ref feedbackBitmap);
                feedbackBitmap = dc.CreateEmptyBitmap(screenSize.Width, screenSize.Height, format: Vortice.DXGI.Format.R16G16B16A16_Float, pixelBytes: 8);
                disposer.Collect(feedbackBitmap);

                disposer.RemoveAndDispose(ref feedbackTmpBitmap);
                feedbackTmpBitmap = dc.CreateNotInitializedBitmap(screenSize.Width, screenSize.Height, format: Vortice.DXGI.Format.R16G16B16A16_Float);
                disposer.Collect(feedbackTmpBitmap);

                outputEffect.SetInput(0, feedbackBitmap, true);
                outputEffect.TransformMatrix = Matrix3x2.CreateTranslation(-screenSize.Width / 2, -screenSize.Height / 2);

                afterImageEffect.SetInput(1, feedbackTmpBitmap, true);
            }

            if(isFirst || this.zoom != zoom)
                zoomEffect.TransformMatrix = Matrix3x2.CreateScale(zoom);
            if (isFirst || this.draw != draw || this.rotation != rotation || this.camera != camera || this.invert != invert || this.centerPoint != centerPoint)
            {
                renderEffect.TransformMatrix =
                    (invert ? Matrix4x4.CreateScale(-1, 1, 1, new Vector3(centerPoint, 0)) : Matrix4x4.Identity)
                    * Matrix4x4.CreateRotationZ(MathF.PI * rotation.Z / 180f)
                    * Matrix4x4.CreateRotationY(MathF.PI * -rotation.Y / 180f)
                    * Matrix4x4.CreateRotationX(MathF.PI * -rotation.X / 180f)
                    * Matrix4x4.CreateTranslation(draw)
                    * camera
                    * new Matrix4x4(
                        1, 0, 0, 0,
                        0, 1, 0, 0,
                        0, 0, 1, -1 / 1000f,
                        0, 0, 0, 1);
            }
            if (isFirst || this.screenSize != screenSize)
                centeringEffect.TransformMatrix = Matrix3x2.CreateTranslation(screenSize.Width / 2f, screenSize.Height / 2f);

            if (isFirst || this.opacity != opacity)
            {
                opacityEffect.Matrix = new Matrix5x4()
                {
                    M11 = 1, M12 = 0, M13 = 0, M14 = 0,
                    M21 = 0, M22 = 1, M23 = 0, M24 = 0,
                    M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                    M41 = 0, M42 = 0, M43 = 0, M44 = (float)opacity,
                    M51 = 0, M52 = 0, M53 = 0, M54 = 0,
                };
            }
            if (isFirst || this.strength != strength)
                afterImageEffect.Strength = (float)strength;
            if (isFirst || this.mode != mode)
                afterImageEffect.Mode = (int)mode;

            SafeTransform3DHelper.Apply(devices.DeviceContext, cropEffect, renderOutput);

            feedbackTmpBitmap.CopyFromBitmap(feedbackBitmap);

            dc.Target = feedbackBitmap;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(afterImageEffect);
            dc.EndDraw();
            dc.Target = null;

            isFirst = false;

            this.frame = frame;
            this.screenSize = screenSize;
            this.draw = draw;
            this.centerPoint = centerPoint;
            this.zoom = zoom;
            this.rotation = rotation;
            this.camera = camera;
            this.opacity = opacity;
            this.invert = invert;

            this.strength = strength;
            this.mode = mode;

            return CreateResultDrawDescription(desc);
        }

        static DrawDescription CreateResultDrawDescription(EffectDescription desc)
        {
            return desc.DrawDescription with
            {
                Draw = new Vector3(),
                Zoom = new Vector2(1.0f, 1.0f),
                Rotation = new Vector3(),
                Camera = Matrix4x4.Identity,
                Opacity = 1.0,
                Invert = false,
            };
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            afterImageEffect = new AfterImageCustomEffect(devices) { InputCount = 2 };
            if(!afterImageEffect.IsEnabled)
            {
                afterImageEffect.Dispose();
                afterImageEffect = null;
                return null;
            }
            disposer.Collect(afterImageEffect);


            zoomEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(zoomEffect);

            cropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);

            renderEffect = new Transform3D(devices.DeviceContext);
            disposer.Collect(renderEffect);

            centeringEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(centeringEffect);

            opacityEffect = new ColorMatrix(devices.DeviceContext);
            disposer.Collect(opacityEffect);

            floodEffect = new Flood(devices.DeviceContext) { Color = new Vector4(0, 0, 0, 0) };
            disposer.Collect(floodEffect);

            compositeEffect = new Composite(devices.DeviceContext) { InputCount = 2 };
            disposer.Collect(compositeEffect);

            using (var output = zoomEffect.Output)
                cropEffect.SetInput(0, output, true);
            using(var output = cropEffect.Output)
                renderEffect.SetInput(0, output, true);
            using(var output = renderEffect.Output)
                centeringEffect.SetInput(0, output, true);
            using (var output = centeringEffect.Output)
                opacityEffect.SetInput(0, output, true);
            using (var output = floodEffect.Output)
                compositeEffect.SetInput(0, output, true);
            using (var output = opacityEffect.Output)
                compositeEffect.SetInput(1, output, true);
            using (var output = compositeEffect.Output)
                afterImageEffect.SetInput(0, output, true);

            renderOutput = renderEffect.Output;
            disposer.Collect(renderOutput);

            outputEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(outputEffect);

            var result = outputEffect.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void setInput(ID2D1Image? input)
        {
            zoomEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            zoomEffect?.SetInput(0, null, true);
            cropEffect?.SetInput(0, null, true);
            renderEffect?.SetInput(0, null, true);
            centeringEffect?.SetInput(0, null, true);
            opacityEffect?.SetInput(0, null, true);
            compositeEffect?.SetInput(0, null, true);
            compositeEffect?.SetInput(1, null, true);
            afterImageEffect?.SetInput(0, null, true);
            afterImageEffect?.SetInput(1, null, true);
            outputEffect?.SetInput(0, null, true);
        }
    }
}