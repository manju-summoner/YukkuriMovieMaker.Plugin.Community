using System.ComponentModel.DataAnnotations;
using System.Numerics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size.Parameter
{
    internal class BothStretchParameter : SizeParameterBase, IWidthParameter, IHeightParameter
    {
        [Display(GroupName = nameof(Texts.ZoomPixel), Name = nameof(Texts.Width), Description = nameof(Texts.Width), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Width { get; } = new Animation(100, 0, 5000);

        [Display(GroupName = nameof(Texts.ZoomPixel), Name = nameof(Texts.Height), Description = nameof(Texts.Height), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Height { get; } = new Animation(100, 0, 5000);

        public override string Label => String.Format(Texts.Width + "{0:0.0}px, " + Texts.Height + "{1:0.0}px", Width.Values[0].Value, Height.Values[0].Value);

        public BothStretchParameter()
        {
        }

        public BothStretchParameter(SharedDataStore? store) : base(store) 
        {
        }

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription, bool dot)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=リサイズ\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"拡大率=100.00\r\n" +
                $"X={Width.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"Y={Height.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"補間なし={(dot ? 1 : 0)}\r\n" +
                $"ドット数でサイズ指定=1\r\n";
        }

        public override Vector2 GetZoom(float sourceWidth, float sourceHeight, EffectDescription effectDescription)
        {
            if (sourceWidth == 0 || sourceHeight == 0) return effectDescription.DrawDescription.Zoom;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            float zoomX = effectDescription.DrawDescription.Zoom.X * (float)Width.GetValue(frame, length, fps) / sourceWidth;
            float zoomY = effectDescription.DrawDescription.Zoom.Y * (float)Height.GetValue(frame, length, fps) / sourceHeight;
            
            return new Vector2(zoomX, zoomY);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Width, Height];

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new WidthSharedData(this));
            store.Save(new HeightSharedData(this));
        }

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<WidthSharedData>() is WidthSharedData widthData)
                widthData.CopyTo(this);
            if (store.Load<HeightSharedData>() is HeightSharedData heightData)
                heightData.CopyTo(this);
        }
    }
}
