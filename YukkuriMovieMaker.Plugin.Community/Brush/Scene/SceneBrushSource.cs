using System.Drawing;
using System.Numerics;
using System.Windows;
using Vortice;
using Vortice.Direct2D1;
using Windows.Win32.UI.Input.Ime;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Scene
{
    internal class SceneBrushSource(IGraphicsDevicesAndContext devices, SceneBrushParameter parameter) : IBrushSource
    {
        readonly IGraphicsDevicesAndContext devices = devices;
        readonly SceneBrushParameter parameter = parameter;
        readonly DisposeCollector disposer = new();

        public ID2D1Brush Brush => brush ?? throw new NullReferenceException();

        Guid sceneId;
        RawRectF bounds;

        ITimelineSource? source;
        ID2D1Bitmap? sourceBitmap;
        ID2D1BitmapBrush? brush;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var dc = devices.DeviceContext;

            var sceneId = parameter.SceneId;

            var playbackRate = parameter.PlaybackRate;
            var contentOffset = parameter.ContentOffset;
            var matrix = parameter.CreateBrushMatrix(desc);
            var extendModeX = parameter.ExtendModeX.ToD2DExtendMode();
            var extendModeY = parameter.ExtendModeY.ToD2DExtendMode();
            var isRemoveBoudalyEnabled = parameter.IsRemoveBoundaryEnabled;
            var isFixSizeEnabled = parameter.IsFixSizeEnabled;

            //シーンが変わったらソースを作り直す
            var targetScene = desc.Scenes.FirstOrDefault(x => x.ID == sceneId);
            if (this.sceneId != sceneId)
            {
                if(source != null)
                    disposer.RemoveAndDispose(ref source);
                if (targetScene != null && targetScene.TryCreateVideoSource(devices, out source))
                    disposer.Collect(source);
            }

            //シーンを更新する
            var time = targetScene != null ? TimeSpan.FromTicks((contentOffset.Ticks + desc.ItemPosition.Time.Ticks) % targetScene.Duration.Time.Ticks) : TimeSpan.Zero;
            source?.Update(time, desc.Usage);

            //描画先の画像サイズを取得
            int width, height;
            Vector2 offset;
            if (source?.Output != null && targetScene != null)
            {
                bounds = dc.GetImageLocalBounds(source.Output);
                if (isFixSizeEnabled)
                {
                    width = targetScene.Width;
                    height = targetScene.Height;
                    offset = new Vector2(width/2f, height/2f);
                }
                else
                {
                    width = Math.Max(1,(int)(bounds.Right - bounds.Left));
                    height = Math.Max(1, (int)(bounds.Bottom - bounds.Top));
                    offset = new Vector2(-bounds.Left, -bounds.Top);
                    if(isRemoveBoudalyEnabled)
                    {
                        if (3 < width)
                        {
                            width -= 2;
                            offset = offset + new Vector2(-1, 0);
                        }
                        if (3 < height)
                        {
                            height -= 2;
                            offset = offset + new Vector2(0, -1);
                        }
                    }
                }
            }
            else
            {
                bounds = new RawRectF(0, 0, 0, 0);
                width = 10;
                height = 10;
                offset = new Vector2(5, 5);
            }

            //描画先の画像を作り直す
            if (sourceBitmap is null || sourceBitmap.PixelSize.Width != width || sourceBitmap.PixelSize.Height != height)
            {
                if(sourceBitmap != null)
                    disposer.RemoveAndDispose(ref sourceBitmap);
                sourceBitmap = dc.CreateEmptyBitmap(width, height);
                disposer.Collect(sourceBitmap);
            }

            //描画先の画像に描画
            dc.Target = sourceBitmap;
            dc.BeginDraw();
            dc.Clear(null);
            if(source != null)
                dc.DrawImage(source.Output, offset);
            dc.EndDraw();
            dc.Target = null;

            //ブラシを作り直す
            if (brush != null)
                disposer.RemoveAndDispose(ref brush);
            brush = dc.CreateBitmapBrush(
                sourceBitmap,
                new BitmapBrushProperties1(extendModeX, extendModeY, InterpolationMode.MultiSampleLinear),
                new BrushProperties(
                    1f,
                    Matrix3x2.CreateTranslation(-sourceBitmap.PixelSize.Width / 2, -sourceBitmap.PixelSize.Height / 2)
                    * matrix));
            disposer.Collect(brush);

            this.sceneId = sceneId;
            return true;

        }

        #region IDisposable Support
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // マネージド状態を破棄します (マネージド オブジェクト)
                    disposer.Dispose();
                }

                // アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~SceneBrushSource()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}