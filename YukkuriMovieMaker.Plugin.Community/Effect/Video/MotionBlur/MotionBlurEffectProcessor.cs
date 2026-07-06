using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.MotionBlur
{
    public class MotionBlurEffectProcessor(IGraphicsDevicesAndContext devices, MotionBlurEffect item) : VideoEffectProcessorBase(devices)
    {
        bool isFirst = true;
        float m11, m12, m21, m22, dx, dy;

        //フレームが連続して描画された場合のみ前フレームの変換として使用する
        long latestFrame = long.MinValue;
        long olderFrame = long.MinValue;
        Matrix3x2? latestTransform;
        Matrix3x2? olderTransform;

        MotionBlurCustomEffect effect = null!;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            //ShaderModel5.0非対応環境用
            if (IsPassThroughEffect) return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var amount = item.Amount.GetValue(frame, length, fps) / 100;

            var current = CreateTransform(effectDescription.DrawDescription);

            if (frame != latestFrame)
            {
                //フレームが進んだ（または飛んだ）場合のみ前フレームの変換を更新する
                //同一フレームの再描画（一時停止中のパラメータ変更等）では前フレームの変換を保持する
                olderFrame = latestFrame;
                olderTransform = latestTransform;
                latestFrame = frame;
            }
            latestTransform = current;

            //プレビュー負荷等でフレームがスキップされた場合は変位を1フレーム分に換算する
            //巻き戻しでは速度が求められないためブラー無しにする
            var delta = CreateDisplacementTransform(olderTransform, current, frame - olderFrame);

            var m11 = (float)((delta.M11 - 1) * amount);
            var m12 = (float)(delta.M12 * amount);
            var m21 = (float)(delta.M21 * amount);
            var m22 = (float)((delta.M22 - 1) * amount);
            var dx = (float)(delta.M31 * amount);
            var dy = (float)(delta.M32 * amount);

            if (isFirst || this.m11 != m11)
                effect.M11 = m11;
            if (isFirst || this.m12 != m12)
                effect.M12 = m12;
            if (isFirst || this.m21 != m21)
                effect.M21 = m21;
            if (isFirst || this.m22 != m22)
                effect.M22 = m22;
            if (isFirst || this.dx != dx)
                effect.Dx = dx;
            if (isFirst || this.dy != dy)
                effect.Dy = dy;

            isFirst = false;
            this.m11 = m11;
            this.m12 = m12;
            this.m21 = m21;
            this.m22 = m22;
            this.dx = dx;
            this.dy = dy;

            return effectDescription.DrawDescription;
        }

        /// <summary>
        /// アイテムの描画変換（DrawingEffectが適用する変換）を2Dアフィン変換に近似する。
        /// 3D回転（X/Y）とカメラは無視し、Z移動は遠近除算（w = 1 - z/1000）による一様スケールとして反映する。
        /// 左右反転はDrawingEffectと同様にズームより前に適用する。
        /// ズームが0等で変換が成立しない場合はnullを返す。
        /// </summary>
        internal static Matrix3x2? CreateTransform(DrawDescription desc)
        {
            var w = 1 - desc.Draw.Z / 1000f;
            if (w < 0.001f)
                return null;
            return
                (desc.Invert ? Matrix3x2.CreateScale(-1, 1, desc.CenterPoint) : Matrix3x2.Identity)
                * Matrix3x2.CreateScale(desc.Zoom)
                * Matrix3x2.CreateRotation(MathF.PI * desc.Rotation.Z / 180f)
                * Matrix3x2.CreateScale(1 / w)
                * Matrix3x2.CreateTranslation(desc.Draw.X / w, desc.Draw.Y / w);
        }

        /// <summary>
        /// 現フレームのローカル座標qを「同じ点が1フレーム前に描画されていた位置（現フレームのローカル座標系）」へ移すアフィン変換を求める。
        /// 変位はq * result - qで得られる。フレームが複数進んでいた場合は変位を線形補間で1フレーム分に換算する（回転は弦の近似）。
        /// 速度が求められない場合（巻き戻し・変換なし等）はIdentity（変位0）を返す。
        /// </summary>
        internal static Matrix3x2 CreateDisplacementTransform(Matrix3x2? older, Matrix3x2? current, long frameGap)
        {
            if (frameGap < 1 || older is not { } o || current is not { } c || !Matrix3x2.Invert(c, out var inverted))
                return Matrix3x2.Identity;
            var displacement = o * inverted;
            return frameGap == 1
                ? displacement
                : Matrix3x2.Lerp(Matrix3x2.Identity, displacement, 1f / frameGap);
        }

        protected override void ClearEffectChain() => effect?.SetInput(0, null, true);

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new MotionBlurCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null!;
                return null;
            }
            disposer.Collect(effect);

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }
    }
}
