using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.BlurMap
{
    internal class BlurMapProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly BlurMapEffect item;

        BlurMapCustomEffect? effect;
        readonly List<GaussianBlur> blurs = [];
        Flood? transparent;
        readonly ID2D1SolidColorBrush transparentBrush;
        IBrushSource? brushSource;
        ID2D1CommandList? brushCommandList;


        bool isFirst = true;
        double blur;
        Type? type;
        bool isFixedSize;
        int inputCount;
        RawRectF bounds;

        public BlurMapProcessor(IGraphicsDevicesAndContext devices, BlurMapEffect item) : base(devices)
        {
            this.item = item;
            this.devices = devices;

            transparentBrush = devices.DeviceContext.CreateSolidColorBrush(new (0, 0, 0, 0));
            disposer.Collect(transparentBrush);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null || transparent is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var blur = item.Blur.GetValue(frame, length, fps);
            var type = item.Brush.Type;
            var isFixedSize = item.IsFixedSize;
            var inputCount = (int)(Math.Max(0, Math.Log2(blur) + 1) + 3);

            bool isChanged = false;
            if (isFirst || this.blur != blur)
            {
                effect.Blur = (float)blur;
                isChanged = true;
            }
            if (isFirst || this.type != type)
            {
                disposer.RemoveAndDispose(ref brushSource);
                brushSource = item.Brush.CreateBrush(devices);
                disposer.Collect(brushSource);
                isChanged = true;
            }
            if (isFirst || this.isFixedSize != isFixedSize)
            {
                foreach (var effect in blurs)
                    effect.BorderMode = isFixedSize ? BorderMode.Hard : BorderMode.Soft;
                isChanged = true;
            }
            if (isFirst || this.inputCount != inputCount)
            {
                //軽量化のため、使用しないぼかしエフェクトは透明画像に置き換える
                for (int i = 2; i < effect.InputCount; i++)
                {
                    using var output = i < inputCount ? blurs[i - 2].Output : transparent.Output;
                    effect.SetInput(i, output, true);
                }
                isChanged = true;
            }
            var dc = devices.DeviceContext;
            var bounds = dc.GetImageLocalBounds(input);
            if (!isFixedSize)
            {
                bounds = new Vortice.RawRectF(
                    bounds.Left - (float)blur,
                    bounds.Top - (float)blur,
                    bounds.Right + (float)blur,
                    bounds.Bottom + (float)blur);
            }
            if (isFirst || !this.bounds.Equals(bounds))
                isChanged = true;

            isChanged |= brushSource?.Update(effectDescription) ?? false;
            if (!isChanged)
                return effectDescription.DrawDescription;



            disposer.RemoveAndDispose(ref brushCommandList);
            brushCommandList = dc.CreateCommandList();
            disposer.Collect(brushCommandList);

            dc.Target = brushCommandList;
            dc.BeginDraw();
            dc.Clear(null);
            if (brushSource is null)
                dc.FillRectangle(bounds, transparentBrush);
            else
                dc.FillRectangle(bounds, brushSource.Brush);
            dc.EndDraw();
            dc.Target = null;
            brushCommandList.Close();
            effect.SetInput(0, brushCommandList, true);

            isFirst = false;
            this.blur = blur;
            this.type = type;
            this.inputCount = inputCount;
            this.isFixedSize = isFixedSize;
            this.bounds = bounds;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new(devices);
            if(!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            for(int i=0;i < effect.InputCount - 2;i++)
            {
                var blur = new GaussianBlur(devices.DeviceContext) 
                {
                    StandardDeviation = MathF.Pow(2, i) / 3f,
                };
                blurs.Add(blur);
                disposer.Collect(blur);
            }

            transparent = new Flood(devices.DeviceContext);
            disposer.Collect(transparent);

            var result = effect.Output;
            disposer.Collect(result);

            return result;
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            effect?.SetInput(1, null, true);
            for (int i = 0; i < blurs.Count; i++)
            {
                effect?.SetInput(i + 2, null, true);
            }
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(1, input, true);
            foreach(var blur in blurs)
            {
                blur.SetInput(0, input, true);
            }
        }
    }
}