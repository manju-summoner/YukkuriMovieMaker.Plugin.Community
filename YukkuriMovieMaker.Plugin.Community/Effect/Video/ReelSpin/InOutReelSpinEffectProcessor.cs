using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReelSpin
{
    internal class InOutReelSpinEffectProcessor(IGraphicsDevicesAndContext devices, InOutReelSpinEffect item) : InOutEffectBase<InOutReelSpinEffect>(devices, item)
    {
        //本体のitem参照は基底InOutEffectBase<T>のprotectedフィールドに解決される（primary ctorパラメータで二重に保持しない）
        readonly IGraphicsDevicesAndContext devices = devices;

        ReelSpinCustomEffect effect = null!;

        bool isFirst = true;
        float rotation, angle, blur;
        int tile;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new ReelSpinCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null!;
                return null;
            }
            disposer.Collect(effect);

            var effectOutput = effect.Output;
            disposer.Collect(effectOutput);
            return effectOutput;
        }
        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }
        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            //ShaderModel5.0非対応環境用
            if (IsPassThroughEffect) return desc;

            var total = effectDescription.ItemDuration.Time.TotalSeconds;
            var sec = effectDescription.ItemPosition.Time.TotalSeconds;
            var fps = effectDescription.FPS;
            var frameSec = fps > 0 ? 1.0 / fps : 0d;
            var effectTimeSeconds = item.EffectTimeSeconds;

            //登場・退場それぞれの進行度（0〜1）をイージング適用して求める
            double EasedIn(double s)
            {
                var rate = item.IsInEffect && effectTimeSeconds > 0 ? Math.Clamp(s / effectTimeSeconds, 0, 1) : 1d;
                return Easing.GetValue(item.EasingType, item.EasingMode, rate);
            }
            double EasedOut(double s)
            {
                var rate = item.IsOutEffect && effectTimeSeconds > 0 ? Math.Clamp((total - s) / effectTimeSeconds, 0, 1) : 1d;
                return Easing.GetValue(item.EasingType, item.EasingMode, rate);
            }
            //登場開始で-laps、中間で0、退場終了で+lapsとなる回転位置。
            //これにより登場中も退場中もコンテンツは常に「方向」パラメータの向きへ流れる
            double RotationAt(double s) => item.Laps * (EasedIn(s) - EasedOut(s));

            var currentEasedIn = EasedIn(sec);
            var currentEasedOut = EasedOut(sec);
            var rotation = (float)(item.Laps * (currentEasedIn - currentEasedOut));
            var angle = (float)(item.Direction * Math.PI / 180);

            //1フレーム分の回転位置の差分からブラー長（周単位）を算出する
            double rotationDelta;
            if (sec + frameSec <= total)
                rotationDelta = RotationAt(sec + frameSec) - RotationAt(sec);
            else
                rotationDelta = RotationAt(sec) - RotationAt(sec - frameSec);
            var blur = (float)(Math.Abs(rotationDelta) * (item.Blur / 100d));
            var tile = item.Tile ? 1 : 0;

            if (isFirst || this.rotation != rotation)
                effect.Rotation = rotation;
            if (isFirst || this.angle != angle)
                effect.Angle = angle;
            if (isFirst || this.blur != blur)
                effect.Blur = blur;
            if (isFirst || this.tile != tile)
                effect.Tile = tile;

            isFirst = false;
            this.rotation = rotation;
            this.angle = angle;
            this.blur = blur;
            this.tile = tile;

            return desc;
        }
    }
}
