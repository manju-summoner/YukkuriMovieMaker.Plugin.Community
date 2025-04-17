using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RadialArrangeGroupItems
{
    internal class RadialArrangeGroupItemsEffectProcessor(IGraphicsDevicesAndContext devices, RadialArrangeGroupItemsEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly RadialArrangeGroupItemsEffect item = item;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var radius = item.Radius.GetValue(frame, length, fps);
            var isRotationSynchronized = item.IsRotationSynchronized;

            var groupIndex = effectDescription.GroupIndex;
            var groupCount = effectDescription.GroupCount;
            if (groupCount == 0)
                return effectDescription.DrawDescription;
            var angle = (float)(2 * Math.PI * groupIndex / groupCount);

            var matrix = Matrix4x4.CreateRotationZ(angle);
            var v = Vector3.Transform(new Vector3(0, (float)-radius, 0), matrix);
            var desc = effectDescription.DrawDescription;
            return desc with
            {
                Draw = desc.Draw + v,
                Rotation = isRotationSynchronized ? desc.Rotation + new Vector3(0, 0, 360* groupIndex / groupCount) : desc.Rotation,
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