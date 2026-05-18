using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YMM4SamplePlugin.Transition.Pixelize
{
    internal sealed class PixelizeTransitionSource : ITransitionSource
    {
        readonly ID2D1Image before;
        readonly ID2D1Image after;
        readonly PixelizeTransitionParameter parameter;

        readonly PixelizeCustomEffect? effect;
        readonly ID2D1Image? effectOutput;

        bool isFirst = true;
        float lastProgress;
        float lastMaxBlockPx;

        public ID2D1Image Output => effectOutput ?? before;

        public PixelizeTransitionSource(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after, PixelizeTransitionParameter parameter)
        {
            this.before = before;
            this.after = after;
            this.parameter = parameter;

            effect = new PixelizeCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return;
            }

            effectOutput = effect.Output;
            effect.SetInput(0, before, true);
            effect.SetInput(1, after, true);
        }

        void ITransitionSource.Update(TimelineItemSourceDescription desc)
        {
            if (effect is null)
                return;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var rawProgress = (double)frame / length;
            var easedProgress = (float)Easing.GetValue(parameter.EasingType, parameter.EasingMode, rawProgress);
            var maxBlockPx = (float)parameter.MaxBlockSize.GetValue(frame, length, fps);

            if (!isFirst && lastProgress == easedProgress && lastMaxBlockPx == maxBlockPx)
                return;

            effect.Progress = easedProgress;
            effect.MaxBlockPx = maxBlockPx;

            isFirst = false;
            lastProgress = easedProgress;
            lastMaxBlockPx = maxBlockPx;
        }

        void IDisposable.Dispose()
        {
            effect?.SetInput(0, null, true);
            effect?.SetInput(1, null, true);
            effectOutput?.Dispose();
            effect?.Dispose();
        }
    }
}
