using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AlphaMask
{
    internal class AlphaMaskEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly AlphaMaskEffect item;
        ColorMatrix? colorMatrixEffect;
        GaussianBlur? blurEffect;
        Vortice.Direct2D1.Effects.AlphaMask? alphaMaskEffect;
        IBrushSource? source;
        ID2D1CommandList? commandList;
        readonly ID2D1SolidColorBrush transparentBrush;

        bool isFirst = true;
        Type? type;
        double blur;
        bool isInverted;
        RawRectF bounds;

        public AlphaMaskEffectProcessor(IGraphicsDevicesAndContext devices, AlphaMaskEffect item) : base(devices)
        {
            this.devices = devices;
            this.item = item;

            transparentBrush = devices.DeviceContext.CreateSolidColorBrush(new (0, 0, 0, 0));
            disposer.Collect(transparentBrush);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (alphaMaskEffect is null || blurEffect is null || colorMatrixEffect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var blur = item.Blur.GetValue(frame, length, fps);
            var isInverted = item.IsInverted;
            var type = item.Brush.Type;

            bool isBrushChanged = false;
            if (isFirst || this.type != type)
            {
                disposer.RemoveAndDispose(ref source);
                source = item.Brush.CreateBrush(devices);
                disposer.Collect(source);
                isBrushChanged = true;
            }
            if (isFirst || this.blur != blur)
                blurEffect.StandardDeviation = (float)blur / 3;
            if(isFirst || this.isInverted != isInverted)
            {
                colorMatrixEffect.Matrix = isInverted
                    ? new()
                    {
                        M11 = 0, M12 = 0, M13 = 0, M14 = 0, 
                        M21 = 0, M22 = 0, M23 = 0, M24 = 0, 
                        M31 = 0, M32 = 0, M33 = 0, M34 = 0, 
                        M41 = 0, M42 = 0, M43 = 0, M44 = -1, 
                        M51 = 0, M52 = 0, M53 = 0, M54 = 1
                    }
                    : new()
                    {
                        M11 = 0, M12 = 0, M13 = 0, M14 = 0, 
                        M21 = 0, M22 = 0, M23 = 0, M24 = 0, 
                        M31 = 0, M32 = 0, M33 = 0, M34 = 0, 
                        M41 = 0, M42 = 0, M43 = 0, M44 = 1, 
                        M51 = 0, M52 = 0, M53 = 0, M54 = 0
                    };
            }

            var dc = devices.DeviceContext;
            var bounds = dc.GetImageLocalBounds(input);
            if (isFirst || !this.bounds.Equals(bounds))
                isBrushChanged = true;

            isBrushChanged |= source?.Update(effectDescription) ?? false;

            if (isBrushChanged)
            {
                disposer.RemoveAndDispose(ref commandList);
                commandList = dc.CreateCommandList();
                disposer.Collect(commandList);

                dc.Target = commandList;
                dc.BeginDraw();
                dc.Clear(null);
                if (source != null)
                    dc.FillRectangle(bounds, source.Brush);
                else
                    dc.FillRectangle(bounds, transparentBrush);
                dc.EndDraw();
                dc.Target = null;
                commandList.Close();

                colorMatrixEffect.SetInput(0, commandList, true);
            }
            isFirst = false;
            this.blur = blur;
            this.type = type;
            this.bounds = bounds;
            this.isInverted = isInverted;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            colorMatrixEffect = new ColorMatrix(devices.DeviceContext);
            disposer.Collect(colorMatrixEffect);

            blurEffect = new GaussianBlur(devices.DeviceContext);
            disposer.Collect(blurEffect);

            alphaMaskEffect = new Vortice.Direct2D1.Effects.AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect);

            using (var output = colorMatrixEffect.Output)
                blurEffect.SetInput(0, output, true);
            using (var output = blurEffect.Output)
                alphaMaskEffect.SetInput(1, output, true);

            var result = alphaMaskEffect.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void setInput(ID2D1Image? input)
        {
            alphaMaskEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            alphaMaskEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(1, null, true);
        }
    }
}