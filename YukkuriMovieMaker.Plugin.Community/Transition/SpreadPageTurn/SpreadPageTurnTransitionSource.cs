using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn
{
    internal sealed class SpreadPageTurnTransitionSource : ITransitionSource
    {
        readonly ID2D1Image after;
        readonly SpreadPageTurnTransitionParameter item;

        readonly SpreadPageTurnCustomEffect? effect;
        readonly ID2D1Image? effectOutput;

        bool isFirst = true;
        float progress, radius, shadow;
        SpreadPageTurnPage page;

        public ID2D1Image Output => effectOutput ?? after;

        public SpreadPageTurnTransitionSource(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after, SpreadPageTurnTransitionParameter item)
        {
            this.after = after;
            this.item = item;

            effect = new SpreadPageTurnCustomEffect(devices);
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

        public void Update(TimelineItemSourceDescription desc)
        {
            if (effect is null)
                return;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var progress = (float)Easing.GetValue(item.EasingType, item.EasingMode, (double)frame / length);
            var radius = (float)item.Radius.GetValue(frame, length, fps);
            var shadow = (float)item.Shadow.GetValue(frame, length, fps) / 100f;
            var page = item.Page;

            if (!isFirst
                && this.progress == progress
                && this.radius == radius
                && this.shadow == shadow
                && this.page == page)
                return;

            effect.Progress = progress;
            effect.Radius = radius;
            effect.Shadow = shadow;
            effect.Page = (int)page;

            isFirst = false;
            this.progress = progress;
            this.radius = radius;
            this.shadow = shadow;
            this.page = page;
        }

        public void Dispose()
        {
            effect?.SetInput(0, null, true);
            effect?.SetInput(1, null, true);
            effectOutput?.Dispose();
            effect?.Dispose();
        }
    }
}
