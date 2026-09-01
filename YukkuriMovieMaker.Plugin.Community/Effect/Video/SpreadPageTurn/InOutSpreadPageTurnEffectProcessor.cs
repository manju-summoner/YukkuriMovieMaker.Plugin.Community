using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn;
using D2D = Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.SpreadPageTurn
{
    internal class InOutSpreadPageTurnEffectProcessor(IGraphicsDevicesAndContext devices, InOutSpreadPageTurnEffect spreadPageTurnItem) : InOutEffectBase<InOutSpreadPageTurnEffect>(devices, spreadPageTurnItem)
    {
        //本体のitem参照は基底InOutEffectBase<T>のprotectedフィールドに解決される（primary ctorパラメータで二重に保持しない）

        readonly IGraphicsDevicesAndContext devices = devices;

        SpreadPageTurnCustomEffect effect = null!;
        D2D.Effects.Flood flood = null!;
        D2D.Effects.Crop crop = null!;

        bool isFirst = true;
        float progress, radius, shadow, backLightness, invDistance;
        SpreadPageTurnPage page;
        SpreadPageTurnStyle style;
        RawRectF inputBounds;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new SpreadPageTurnCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null!;
                return null;
            }
            disposer.Collect(effect);

            //単一入力のエフェクト版のため、めくったページの裏面は表面を白くした紙にする
            effect.BackMode = 1;

            //めくった先(after)は透明。ページ（＝入力画像の矩形）の外へはみ出さないよう、
            //Updateで入力の矩形に合わせてCropする
            flood = new D2D.Effects.Flood(devices.DeviceContext)
            {
                Color = new Vector4(0f, 0f, 0f, 0f)
            };
            disposer.Collect(flood);

            crop = new D2D.Effects.Crop(devices.DeviceContext);
            disposer.Collect(crop);

            using (var output = flood.Output)
                crop.SetInput(0, output, true);
            crop.Rectangle = new Vector4(0f, 0f, 1f, 1f);

            using (var output = crop.Output)
                effect.SetInput(1, output, true);

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
            effect?.SetInput(1, null, true);
            crop?.SetInput(0, null, true);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            //ShaderModel5.0非対応環境用
            if (IsPassThroughEffect) return desc;

            //アイテムの端で1(反対側の半面に完全に折り畳まれた状態)、中間で0(元の表示)。
            //登場・退場のうち近い方の進行度を採用する（基底InOutEffectBaseのGetEasingValue = 1 - eased）。
            var progress = (float)GetEasingValue(effectDescription, 1, 0);
            var radius = (float)item.Radius;
            var invDistance = SpreadPageTurnCustomEffect.CalculateInvDistance(item.Fov, effectDescription.ScreenSize.Height);
            var shadow = (float)item.Shadow / 100f;
            var backLightness = (float)item.BackLightness / 100f;
            var page = item.Page;
            var style = item.Style;

            //after(透明)の矩形を入力画像の矩形に合わせる
            if (input is not null)
            {
                var bounds = devices.DeviceContext.GetImageLocalBounds(input);
                if (isFirst || !inputBounds.Equals(bounds))
                    crop.Rectangle = new Vector4(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                inputBounds = bounds;
            }

            if (isFirst || this.progress != progress)
                effect.Progress = progress;
            if (isFirst || this.radius != radius)
                effect.Radius = radius;
            if (isFirst || this.invDistance != invDistance)
                effect.InvDistance = invDistance;
            if (isFirst || this.shadow != shadow)
                effect.Shadow = shadow;
            if (isFirst || this.backLightness != backLightness)
                effect.BackLightness = backLightness;
            if (isFirst || this.page != page)
                effect.Page = (int)page;
            if (isFirst || this.style != style)
                //enum値はShowPropertyEditorWhenの都合で1始まりのため、シェーダーの0/1へ明示的に写像する
                effect.Style = style == SpreadPageTurnStyle.Fold ? 1 : 0;

            isFirst = false;
            this.progress = progress;
            this.radius = radius;
            this.invDistance = invDistance;
            this.shadow = shadow;
            this.backLightness = backLightness;
            this.page = page;
            this.style = style;

            return desc;
        }
    }
}
