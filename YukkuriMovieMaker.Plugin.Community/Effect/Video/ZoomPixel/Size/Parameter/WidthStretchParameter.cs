using System.ComponentModel.DataAnnotations;
using System.Numerics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size.Parameter
{
    internal class WidthStretchParameter : SizeParameterBase, IWidthParameter
    {
        [Display(GroupName = nameof(Texts.ZoomPixel), Name = nameof(Texts.Width), Description = nameof(Texts.Width), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Width { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        public override string Label => $"{Texts.Width}{Width.Values[0].Value:F1}px";

        public WidthStretchParameter() 
        {
        }

        public WidthStretchParameter(SharedDataStore? store) : base(store)
        {
        }

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription, bool dot) 
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"_track0={Width.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=拡大縮小（ピクセル）@YMM4-未実装\r\n" +
                $"param=" +
                    $"local mode=1;" +
                    $"local dot={(dot ? 1 : 0)};" +
                    $"\r\n";
        }

        public override Vector2 GetZoom(float sourceWidth, float sourceHeight, EffectDescription effectDescription)
        {
            if (sourceWidth == 0) return effectDescription.DrawDescription.Zoom;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            float zoomX = effectDescription.DrawDescription.Zoom.X * (float)Width.GetValue(frame, length, fps) / sourceWidth;
            float zoomY = effectDescription.DrawDescription.Zoom.Y;

            return new Vector2(zoomX, zoomY);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Width];

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new WidthSharedData(this));
        }

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<WidthSharedData>() is WidthSharedData widthData)
                widthData.CopyTo(this);
        }
    }
}
