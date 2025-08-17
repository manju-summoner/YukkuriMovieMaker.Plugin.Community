using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Stretch
{
    internal class StretchEffectProcessor(IGraphicsDevicesAndContext devices, StretchEffect item) : VideoEffectProcessorBase(devices)
    {
        StretchCustomEffect? stretchCustomEffect;

        bool isFirst = true;
        bool isCentering;
        float x, y, angle, stretchLength, range;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || stretchCustomEffect is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var isCentering = item.IsCentering;
            var x = (float)item.X.GetValue(frame, length, fps);
            var y = (float)item.Y.GetValue(frame, length, fps);
            var angle = (float)(item.Angle.GetValue(frame, length, fps) / 180 * Math.PI);
            var stretchLength = (float)item.StretchLength.GetValue(frame, length, fps);
            var range = (float)(item.Range.GetValue(frame, length, fps));

            if (isFirst || this.isCentering != isCentering)
                stretchCustomEffect.IsCentering = isCentering;
            if (isFirst || this.x != x)
                stretchCustomEffect.X = x;
            if (isFirst || this.y != y)
                stretchCustomEffect.Y = y;
            if (isFirst || this.angle != angle)
                stretchCustomEffect.Angle = angle;
            if (isFirst || this.stretchLength != stretchLength)
                stretchCustomEffect.StretchLength = stretchLength;
            if (isFirst || this.range != range)
                stretchCustomEffect.Range = range;

            isFirst = false;
            this.isCentering = isCentering;
            this.x = x;
            this.y = y;
            this.angle = angle;
            this.stretchLength = stretchLength;
            this.range = range;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            stretchCustomEffect = new(devices);
            if (!stretchCustomEffect.IsEnabled)
            {
                stretchCustomEffect.Dispose();
                stretchCustomEffect = null;
                return null;
            }
            disposer.Collect(stretchCustomEffect);

            var output = stretchCustomEffect.Output;
            disposer.Collect(output);

            return output;
        }

        protected override void ClearEffectChain()
        {
            stretchCustomEffect?.SetInput(0, null, true);
        }

        protected override void setInput(ID2D1Image? input)
        {
            stretchCustomEffect?.SetInput(0, input, true);
        }
    }
}
