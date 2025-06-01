using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Rotate
{
    internal class AudioVolumeRotateEffectProcessor(IGraphicsDevicesAndContext devices, AudioVolumeRotateEffect item) : AudioVolumeVideoEffectProcessorBase(devices, item)
    {
        readonly IGraphicsDevicesAndContext devices = devices;

        AffineTransform2D? transform2D;
        Crop? cropEffect;
        Transform3D? transform3D;
        ID2D1Image? renderOutput;

        bool isFirst = true;
        Matrix3x2 matrix2D;
        Matrix4x4 matrix3D;
        AffineTransform2DInterpolationMode interpolationMode2D;
        Transform3DInterpolationMode interpolationMode3D;

        public override DrawDescription Update(EffectDescription effectDescription, double audioVolume)
        {
            if (transform2D is null || cropEffect is null || transform3D is null || renderOutput is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var x = (float)(item.X.GetValue(frame, length, fps) * audioVolume);
            var y = (float)(item.Y.GetValue(frame, length, fps) * audioVolume);
            var z = (float)(item.Z.GetValue(frame, length, fps) * audioVolume);
            var is3D = item.Is3D;

            var drawDescription = effectDescription.DrawDescription;
            var interpolationMode2D = drawDescription.ZoomInterpolationMode.ToTransform2D();
            var interpolationMode3D = drawDescription.ZoomInterpolationMode.ToTransform3D();
            var zoom = drawDescription.Zoom;

            Matrix3x2 matrix2D;
            Matrix4x4 matrix3D;
            if (is3D)
            {
                matrix2D = Matrix3x2.Identity;
                matrix3D = Matrix4x4.Identity;
            }
            else
            {
                matrix2D = Matrix3x2.CreateScale(zoom);
                matrix3D = Matrix4x4.CreateRotationZ(z / 180f * MathF.PI) 
                    * Matrix4x4.CreateRotationY(-y / 180f * MathF.PI) 
                    * Matrix4x4.CreateRotationX(-x / 180f * MathF.PI) 
                    * new Matrix4x4(
                        1f, 0f, 0f, 0f,
                        0f, 1f, 0f, 0f,
                        0f, 0f, 1f, -0.001f,
                        0f, 0f, 0f, 1f);
            }

            if (isFirst || this.matrix2D != matrix2D)
                transform2D.TransformMatrix = matrix2D;
            if (isFirst || this.matrix3D != matrix3D)
                transform3D.TransformMatrix = matrix3D;
            if (isFirst || this.interpolationMode2D != interpolationMode2D)
                transform2D.InterPolationMode = interpolationMode2D;
            if (isFirst || this.interpolationMode3D != interpolationMode3D)
                transform3D.InterPolationMode = interpolationMode3D;

            cropEffect.Rectangle = new Vector4(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);
            RawRectF imageLocalBounds = devices.DeviceContext.GetImageLocalBounds(renderOutput);
            if ((double)imageLocalBounds.Left == (double)int.MinValue || (double)imageLocalBounds.Top == (double)int.MinValue || (double)imageLocalBounds.Right == 2147483648.0 || (double)imageLocalBounds.Bottom == 2147483648.0)
                cropEffect.Rectangle = new(-2048f, -2048f, 2048f, 2048f);
            
            isFirst = false;
            this.matrix2D = matrix2D;
            this.matrix3D = matrix3D;
            this.interpolationMode2D = interpolationMode2D;
            this.interpolationMode3D = interpolationMode3D;

            return drawDescription with
            {
                Rotation = is3D ? drawDescription.Rotation + new Vector3(x, y, z) : drawDescription.Rotation,
                Zoom = is3D ? drawDescription.Zoom : new Vector2(1f, 1f)
            };
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            transform2D = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(transform2D);

            cropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);

            transform3D = new Transform3D(devices.DeviceContext);
            disposer.Collect(transform3D);

            using (var output = transform2D.Output) 
            {
                cropEffect.SetInput(0, output, true);
            }

            using (var output = cropEffect.Output)
            {
                transform3D.SetInput(0, output, true);
            }

            renderOutput = transform3D.Output;
            disposer.Collect(renderOutput);
            return renderOutput;
        }

        protected override void setInput(ID2D1Image? input)
        {
            transform2D?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            transform2D?.SetInput(0, null, true);
            cropEffect?.SetInput(0, null, true);
            transform3D?.SetInput(0, null, true);
        }
    }
}
