using Vortice.Direct2D1;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RotationPerSecond
{
    internal class RotationPerSecondProcessor : IVideoEffectProcessor
    {
        readonly RotationPerSecond effect;

        ID2D1Image? input;

        int cachedFrame = -1;
        double accumX, accumY, accumZ;

        public ID2D1Image Output =>
            input ?? throw new System.InvalidOperationException("Input が設定されていません。");

        public RotationPerSecondProcessor(RotationPerSecond effect)
        {
            this.effect = effect;
        }

        public void SetInput(ID2D1Image? input) => this.input = input;
        public void ClearInput() => input = null;

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            int startFrame;
            if (cachedFrame < 0 || frame < cachedFrame)
            {

                accumX = effect.OffsetX.GetValue(0, length, fps);
                accumY = effect.OffsetY.GetValue(0, length, fps);
                accumZ = effect.OffsetZ.GetValue(0, length, fps);
                startFrame = 1;
            }
            else
            {
                startFrame = cachedFrame + 1;
            }

            for (int i = startFrame; i <= frame; i++)
            {
                var seconds = effect.Seconds.GetValue(i, length, fps);
                var cycleFrames = seconds * fps;

                if (cycleFrames > 0.0)
                {
                    accumX += effect.RotationXCount.GetValue(i, length, fps) * 360.0 / cycleFrames;
                    accumY += effect.RotationYCount.GetValue(i, length, fps) * 360.0 / cycleFrames;
                    accumZ += effect.RotationZCount.GetValue(i, length, fps) * 360.0 / cycleFrames;
                }
            }

            cachedFrame = frame;

            var desc = effectDescription.DrawDescription;
            var oldRot = desc.Rotation;

            return desc with
            {
                Rotation = oldRot with
                {
                    X = (float)(oldRot.X + accumX),
                    Y = (float)(oldRot.Y + accumY),
                    Z = (float)(oldRot.Z + accumZ),
                }
            };
        }

        public void Dispose()
        {
            input = null;
        }
    }
}