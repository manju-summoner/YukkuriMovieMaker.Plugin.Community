using System.Numerics;
using System.Windows;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.TilingGroupItems
{
    internal class TilingGroupItemsEffectProcessor(IGraphicsDevicesAndContext devices, TilingGroupItemsEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly IGraphicsDevicesAndContext devices = devices;
        readonly TilingGroupItemsEffect item = item;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var groupIndex = effectDescription.GroupIndex;
            var groupCount = effectDescription.GroupCount;

            float reverse = 1;
            if (item.IsEndAligned)
            {
                groupIndex = groupCount - groupIndex - 1;
                reverse = -1;
            }

            var columns = (int)item.Wrap.GetValue(frame, length, fps);
            var rows = (int)Math.Ceiling((double)groupCount / columns);
            var columnIndex = groupIndex % columns;
            var rowIndex = groupIndex / columns;
            if (rowIndex == rows - 1)
                columns -= columns * rows - groupCount;

            if (item.IsVertical)
                (columns, rows, columnIndex, rowIndex) = (rows, columns, rowIndex, columnIndex);

            var screen = effectDescription.ScreenSize;
            var cwllWidth = (double)screen.Width / columns;
            var cwllHeight = (double)screen.Height / rows;
            var dx = (float)(cwllWidth * columnIndex + cwllWidth / 2 - (double)screen.Width / 2) * reverse;
            var dy = (float)(cwllHeight * rowIndex + cwllHeight / 2 - (double)screen.Height / 2) * reverse;

            var bounds = devices.DeviceContext.GetImageLocalBounds(input);
            double scale = 1;
            if (bounds.Left != 0)
                scale = Math.Min(scale, cwllWidth / 2 / Math.Abs(bounds.Left));
            if (bounds.Right != 0)
                scale = Math.Min(scale, cwllWidth / 2 / Math.Abs(bounds.Right));
            if (bounds.Top != 0)
                scale = Math.Min(scale, cwllHeight / 2 / Math.Abs(bounds.Top));
            if (bounds.Bottom != 0)
                scale = Math.Min(scale, cwllHeight / 2 / Math.Abs(bounds.Bottom));

            var desc = effectDescription.DrawDescription;
            return desc with
            {
                Draw = desc.Draw + new Vector3(dx, dy, 0),
                Zoom = desc.Zoom * new Vector2((float)scale, (float)scale),
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