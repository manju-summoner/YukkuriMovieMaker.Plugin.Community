using MathNet.Numerics.Random;
using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CameraShake
{
    internal class CameraShakeEffectProcessor(IGraphicsDevicesAndContext devices, CameraShakeEffect item) : VideoEffectProcessorBase(devices)
    {
        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var span = item.Span.GetValue(frame, length, fps);
            var x = (float)(item.X.GetValue(frame, length, fps) * GetCenteringRandomValue(span, frame, fps, 0));
            var y = (float)(item.Y.GetValue(frame, length, fps) * GetCenteringRandomValue(span, frame, fps, 1));
            var z = (float)(item.Z.GetValue(frame, length, fps) * GetCenteringRandomValue(span, frame, fps, 2));
            var yaw = (float)(item.Yaw.GetValue(frame, length, fps) / 180 * Math.PI * GetCenteringRandomValue(span, frame, fps, 3));
            var pitch = (float)(item.Pitch.GetValue(frame, length, fps) / 180 * Math.PI * GetCenteringRandomValue(span, frame, fps, 4));
            var roll = (float)(item.Roll.GetValue(frame, length, fps) / 180 * Math.PI * GetCenteringRandomValue(span, frame, fps, 5));

            var drawDescription = effectDescription.DrawDescription;
            //カメラの回転は視点を中心に行う。視点の位置は前段の視野角の方式で変わる
            var eye = new Vector3(0, 0, CameraFinalizer.GetEyeDistance(drawDescription));
            var camera = 
                drawDescription.Camera 
                * Matrix4x4.CreateTranslation(x, y, z)
                * Matrix4x4.CreateRotationY(yaw, eye)
                * Matrix4x4.CreateRotationX(pitch, eye)
                * Matrix4x4.CreateRotationZ(roll, eye);

            return drawDescription with { Camera = camera };
        }

        private double GetCenteringRandomValue(double span, int frame, int fps, int parameterID)
        {
            double random = span != 0.0 ? Animation.GetRandomMoveRate(item, parameterID, frame, fps, span) : new MersenneTwister(item.GetHashCode() / (parameterID + 1) + frame).NextDouble();
            return random - 0.5;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            return null;
        }

        protected override void setInput(ID2D1Image? input)
        {
        }

        protected override void ClearEffectChain()
        {
        }
    }
}
