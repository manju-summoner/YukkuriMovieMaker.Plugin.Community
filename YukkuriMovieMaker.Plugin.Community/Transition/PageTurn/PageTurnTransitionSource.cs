using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.PageTurn
{
    internal sealed class PageTurnTransitionSource : ITransitionSource
    {
        readonly ID2D1Image after;
        readonly PageTurnTransitionParameter item;

        readonly PageTurnCustomEffect? effect;
        readonly ID2D1Image? effectOutput;

        bool isFirst = true;
        float progress, radius, shadow, backLightness;
        PageTurnOrigin origin;

        public ID2D1Image Output => effectOutput ?? after;

        public PageTurnTransitionSource(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after, PageTurnTransitionParameter item)
        {
            this.after = after;
            this.item = item;

            effect = new PageTurnCustomEffect(devices);
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
            var backLightness = (float)item.BackLightness.GetValue(frame, length, fps) / 100f;
            var origin = item.Origin;

            if (!isFirst
                && this.progress == progress
                && this.radius == radius
                && this.shadow == shadow
                && this.backLightness == backLightness
                && this.origin == origin)
                return;

            effect.Progress = progress;
            effect.Radius = radius;
            effect.Shadow = shadow;
            effect.BackLightness = backLightness;
            effect.Origin = (int)origin;

            isFirst = false;
            this.progress = progress;
            this.radius = radius;
            this.shadow = shadow;
            this.backLightness = backLightness;
            this.origin = origin;
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
