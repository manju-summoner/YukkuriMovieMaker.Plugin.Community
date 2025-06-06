using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ArrangeGroupItems
{
    internal class ArrangeGroupItemsEffectProcessor(IGraphicsDevicesAndContext devices, ArrangeGroupItemsEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly ArrangeGroupItemsEffect item = item;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var interval = item.Interval.GetValue(frame, length, fps);
            var angle = item.Angle.GetValue(frame, length, fps);
            var isCentering = item.IsCentering;

            var offset = interval * effectDescription.GroupIndex ;
            if(isCentering)
                offset -= (interval * (effectDescription.GroupCount - 1)) / 2;

            var matrix = Matrix4x4.CreateRotationZ((float)(angle * Math.PI / 180));
            var v = Vector3.Transform(new Vector3((float)offset,0,0), matrix);
            var desc = effectDescription.DrawDescription;
            return desc with
            {
                Draw = desc.Draw + v
            };
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