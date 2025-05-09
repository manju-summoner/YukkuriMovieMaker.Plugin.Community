﻿using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
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
        ID2D1Brush? brush;

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
            if (source?.Output != null && targetScene != null)
            {
                if (isFixSizeEnabled)
                {
                    var width = targetScene.Width;
                    var height = targetScene.Height;
                    bounds = new RawRectF(-width / 2f, -height / 2f, width / 2f, height / 2f);
                }
                else
                {
                    bounds = dc.GetImageLocalBounds(source.Output);
                    if(isRemoveBoudalyEnabled)
                    {
                        if (3 < bounds.Right - bounds.Left)
                        {
                            bounds = new RawRectF(bounds.Left + 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
                        }
                        if (3 < bounds.Bottom - bounds.Top)
                        {
                            bounds = new RawRectF(bounds.Left, bounds.Top + 1, bounds.Right, bounds.Bottom - 1);
                        }
                    }
                }
            }
            else
            {
                bounds = new RawRectF(0, 0, 0, 0);
            }

            //ブラシを作り直す
            if (brush != null)
                disposer.RemoveAndDispose(ref brush);
            if (source?.Output != null)
            {
                brush = dc.CreateImageBrush(
                    source.Output,
                    new ImageBrushProperties(bounds, extendModeX, extendModeY, InterpolationMode.MultiSampleLinear),
                    new BrushProperties(
                        1f,
                        Matrix3x2.CreateTranslation(bounds.Left,bounds.Top) * matrix));
            }
            else
            {
                brush = dc.CreateSolidColorBrush(new Vortice.Mathematics.Color4(0f, 0f, 0f, 0f));
            }
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