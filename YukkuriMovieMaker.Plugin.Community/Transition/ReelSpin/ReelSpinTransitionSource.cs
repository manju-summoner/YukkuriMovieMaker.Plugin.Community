using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.ReelSpin
{
    internal sealed class ReelSpinTransitionSource : ITransitionSource
    {
        readonly ID2D1Image after;
        readonly ReelSpinTransitionParameter item;

        readonly ReelSpinTransitionCustomEffect? effect;
        readonly ID2D1Image? effectOutput;

        bool isFirst = true;
        float travel, angle, blur, laps;
        int pattern;
        int tile;
        float screenWidth, screenHeight;

        public ID2D1Image Output => effectOutput ?? after;

        public ReelSpinTransitionSource(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after, ReelSpinTransitionParameter item)
        {
            this.after = after;
            this.item = item;

            effect = new ReelSpinTransitionCustomEffect(devices);
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

            var p = Easing.GetValue(item.EasingType, item.EasingMode, (double)frame / length);
            var pNext = Easing.GetValue(item.EasingType, item.EasingMode, Math.Min(1.0, (double)(frame + 1) / length));
            //回転数は整数として扱う（スライダーはF0表示だがアニメーション補間で端数になりうるため丸める）
            var laps = (float)Math.Max(1, Math.Round(item.Laps.GetValue(frame, length, fps)));
            //回転数1で直行（beforeの次のafterに着地）、以降1増えるごとに1周（2枚ぶん）余分に回る
            var factor = laps * 2 - 1;

            var travel = (float)(p * factor);
            var angle = (float)(item.Direction.GetValue(frame, length, fps) * Math.PI / 180.0);
            var blur = (float)(Math.Abs(pNext - p) * factor * (item.Blur.GetValue(frame, length, fps) / 100.0));
            var pattern = (int)item.Pattern;
            var tile = item.Tile ? 1 : 0;
            var screenWidth = (float)desc.ScreenSize.Width;
            var screenHeight = (float)desc.ScreenSize.Height;

            if (!isFirst
                && this.travel == travel
                && this.angle == angle
                && this.blur == blur
                && this.laps == laps
                && this.pattern == pattern
                && this.tile == tile
                && this.screenWidth == screenWidth
                && this.screenHeight == screenHeight)
                return;

            effect.Travel = travel;
            effect.Angle = angle;
            effect.Blur = blur;
            effect.Laps = laps;
            effect.Pattern = pattern;
            effect.Tile = tile;
            effect.ScreenWidth = screenWidth;
            effect.ScreenHeight = screenHeight;

            isFirst = false;
            this.travel = travel;
            this.angle = angle;
            this.blur = blur;
            this.laps = laps;
            this.pattern = pattern;
            this.tile = tile;
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;
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
