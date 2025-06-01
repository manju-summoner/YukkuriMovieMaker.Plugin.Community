using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Skew
{
    internal class AudioVolumeSkewEffectProcessor(IGraphicsDevicesAndContext devices, AudioVolumeSkewEffect item) : AudioVolumeVideoEffectProcessorBase(devices, item)
    {
        readonly IGraphicsDevicesAndContext devices = devices;

        AffineTransform2D? transform2D;

        bool isFirst = true;
        float angleX,angleY,centerX,centerY;
        AffineTransform2DInterpolationMode interpolationMode;

        public override DrawDescription Update(EffectDescription effectDescription, double audioVolume)
        {
            if (transform2D is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var angleX = (float)(item.AngleX.GetValue(frame, length, fps) * audioVolume);
            var angleY = (float)(item.AngleY.GetValue(frame,length, fps) * audioVolume);
            var interpolationMode = effectDescription.DrawDescription.ZoomInterpolationMode.ToTransform2D();
            float centerX, centerY;
            if (item.CenterPoint == SkewCenterPoint.Center)
            {
                centerX = 0f;
                centerY = 0f;
            }
            else if (item.CenterPoint == SkewCenterPoint.Custom)
            {
                centerX = (float)item.CenterX.GetValue(frame, length, fps);
                centerY = (float)item.CenterX.GetValue(frame, length, fps);
            }
            else
            {
                RawRectF imageLocalBounds = devices.DeviceContext.GetImageLocalBounds(input);
                if (item.CenterPoint == SkewCenterPoint.Top)
                {
                    centerX = 0f;
                    centerY = imageLocalBounds.Top;
                }
                else if (item.CenterPoint == SkewCenterPoint.Bottom)
                {
                    centerX = 0f;
                    centerY = imageLocalBounds.Bottom;
                }
                else if (item.CenterPoint == SkewCenterPoint.Left)
                {
                    centerX = imageLocalBounds.Left;
                    centerY = 0f;
                }
                else if (item.CenterPoint == SkewCenterPoint.Right)
                {
                    centerX = imageLocalBounds.Right;
                    centerY = 0f;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (isFirst || this.angleX != angleX || this.angleY != angleY || this.centerX != centerX || this.centerY != centerY)
                transform2D.TransformMatrix = Matrix3x2.CreateSkew(angleX / 180f * MathF.PI, angleY / 180f * MathF.PI, new(centerX, centerY));

            if (isFirst || this.interpolationMode != interpolationMode)
                transform2D.InterPolationMode = interpolationMode;

            isFirst = false;
            this.angleX = angleX;
            this.angleY = angleY;
            this.centerX = centerX;
            this.centerY = centerY;
            this.interpolationMode = interpolationMode;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            transform2D = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(transform2D);

            ID2D1Image output = transform2D.Output;
            disposer.Collect(output);

            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            transform2D?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            transform2D?.SetInput(0, null, true);
        }
    }
}
