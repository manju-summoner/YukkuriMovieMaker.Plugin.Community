using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReelSpin
{
    internal class ReelSpinEffectProcessor(IGraphicsDevicesAndContext devices, ReelSpinEffect item) : VideoEffectProcessorBase(devices)
    {
        bool isFirstUpdate = true;
        float rotation;
        float angle;
        float blur;
        int tile;

        ReelSpinCustomEffect? reelSpinEffect;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            reelSpinEffect = new(devices);
            if (!reelSpinEffect.IsEnabled)
            {
                reelSpinEffect.Dispose();
                reelSpinEffect = null;
                return null;
            }
            disposer.Collect(reelSpinEffect);

            var output = reelSpinEffect.Output;
            disposer.Collect(output);
            return output;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || reelSpinEffect is null) return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            //回転位置は%（100%で1周）。シェーダーへは周単位で渡す
            var rotation = (float)(item.Rotation.GetValue(frame, length, fps) / 100);
            var angle = (float)(item.Direction.GetValue(frame, length, fps) * Math.PI / 180);

            //1フレーム分の回転位置の差分からブラー長（周単位）を算出する
            float rotationDelta;
            if (frame + 1 <= length - 1)
                rotationDelta = (float)((item.Rotation.GetValue(frame + 1, length, fps) - item.Rotation.GetValue(frame, length, fps)) / 100);
            else if (length > 1)
                rotationDelta = (float)((item.Rotation.GetValue(frame, length, fps) - item.Rotation.GetValue(frame - 1, length, fps)) / 100);
            else
                rotationDelta = 0f;

            var blur = MathF.Abs(rotationDelta) * (float)(item.Blur.GetValue(frame, length, fps) / 100f);
            var tile = item.Tile ? 1 : 0;

            if (isFirstUpdate || this.rotation != rotation)
                reelSpinEffect.Rotation = rotation;
            if (isFirstUpdate || this.angle != angle)
                reelSpinEffect.Angle = angle;
            if (isFirstUpdate || this.blur != blur)
                reelSpinEffect.Blur = blur;
            if (isFirstUpdate || this.tile != tile)
                reelSpinEffect.Tile = tile;

            isFirstUpdate = false;
            this.rotation = rotation;
            this.angle = angle;
            this.blur = blur;
            this.tile = tile;

            return effectDescription.DrawDescription;
        }

        protected override void setInput(ID2D1Image? input)
        {
            reelSpinEffect?.SetInput(0, input, true);
        }
        protected override void ClearEffectChain()
        {
            SetInput(null);
        }

    }
}
