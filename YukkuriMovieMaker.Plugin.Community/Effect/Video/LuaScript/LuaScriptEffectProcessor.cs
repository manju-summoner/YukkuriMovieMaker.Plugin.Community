using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaScriptEffectProcessor(IGraphicsDevicesAndContext devices, LuaScriptEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly record struct RenderKey(
            int Frame,
            double Time,
            int Length,
            int Fps,
            double Track0,
            double Track1,
            double Track2,
            double Track3,
            string Script,
            TimelineSourceUsage Usage,
            Guid SceneId,
            int TimelineFrame,
            double TimelineTime,
            int SceneWidth,
            int SceneHeight,
            int Layer,
            int InputIndex,
            int InputCount,
            int GroupIndex,
            int GroupCount,
            int TimelineTotalFrame,
            double TimelineTotalTime,
            DrawDescription InputDesc
        );

        private readonly LuaScriptEngine _engine = new();

        private GraphicsDevicesAndContext? _ownCtx;

        private ID2D1Bitmap1? _renderTarget;
        private ID2D1Bitmap1? _stagingBitmap;
        private ID2D1Bitmap1? _outputBitmap;
        private AffineTransform2D? _transformEffect;
        private byte[]? _pixelBuffer;
        private int _bitmapWidth;
        private int _bitmapHeight;
        private RawRectF _cachedBounds;

        private bool _isFirst = true;
        private ID2D1Image? _cachedInput;
        private RenderKey _cachedKey;
        private DrawDescription? _cachedOutputDesc;
        private bool _cachedPixelsModified;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _ownCtx = new GraphicsDevicesAndContext(devices);
            disposer.Collect(_ownCtx);
            return null;
        }

        protected override void setInput(ID2D1Image? input)
        {
            if (!ReferenceEquals(_cachedInput, input))
                _isFirst = true;
            _cachedInput = input;
        }

        protected override void ClearEffectChain()
        {
            effectOutput = null;
        }

        public override DrawDescription Update(EffectDescription desc)
        {
            if (input is null || _ownCtx is null)
                return desc.DrawDescription;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var time = desc.ItemPosition.Time.TotalSeconds;
            var script = item.Script ?? string.Empty;

            var t0 = item.Track0.GetValue(frame, length, fps);
            var t1 = item.Track1.GetValue(frame, length, fps);
            var t2 = item.Track2.GetValue(frame, length, fps);
            var t3 = item.Track3.GetValue(frame, length, fps);

            var inDesc = desc.DrawDescription;

            var key = new RenderKey(
                frame, time, length, fps,
                t0, t1, t2, t3,
                script, desc.Usage, desc.SceneId,
                desc.TimelinePosition.Frame,
                desc.TimelinePosition.Time.TotalSeconds,
                desc.ScreenSize.Width, desc.ScreenSize.Height,
                desc.Layer, desc.InputIndex, desc.InputCount,
                desc.GroupIndex, desc.GroupCount,
                desc.TimelineDuration.Frame,
                desc.TimelineDuration.Time.TotalSeconds,
                inDesc);

            if (!_isFirst && key == _cachedKey)
            {
                effectOutput = _cachedPixelsModified ? _transformEffect?.Output : null;
                return _cachedOutputDesc ?? inDesc;
            }

            var bounds = _ownCtx.DeviceContext.GetImageLocalBounds(input);
            int imgW = Math.Max(1, (int)Math.Ceiling(bounds.Right - bounds.Left));
            int imgH = Math.Max(1, (int)Math.Ceiling(bounds.Bottom - bounds.Top));

            var ctx = BuildContext(in key, imgW, imgH);
            ctx.SetPixelLoader(() => LoadInputPixels(bounds, imgW, imgH));

            DrawDescription outDesc;
            bool pixelsModified = false;

            try
            {
                _engine.Execute(script, ctx);

                if (ctx.IsPixelsDirty)
                {
                    EnsureBitmaps(imgW, imgH);
                    WritePixelsToOutput(ctx.GetPixelBuffer()!, imgW);
                    UpdateTransformEffect(bounds);
                    effectOutput = _transformEffect!.Output;
                    pixelsModified = true;
                }
                else
                {
                    effectOutput = null;
                }

                outDesc = BuildOutputDesc(inDesc, ctx);
            }
            catch (LuaScriptException ex)
            {
                effectOutput = null;
                outDesc = inDesc;
                Log.Default.Write(ex.Message, ex);
            }

            _isFirst = false;
            _cachedKey = key;
            _cachedOutputDesc = outDesc;
            _cachedPixelsModified = pixelsModified;

            return outDesc;
        }

        private static AviUtlScriptContext BuildContext(in RenderKey key, int imgW, int imgH)
        {
            var zoom = key.InputDesc.Zoom;
            double zoomAvg = (zoom.X + zoom.Y) / 2d;
            double aspect = zoomAvg > 0d
                ? (zoom.X - zoom.Y) / (zoom.X + zoom.Y)
                : 0d;

            return new AviUtlScriptContext
            {
                ImageWidth = imgW,
                ImageHeight = imgH,
                X = key.InputDesc.Draw.X,
                Y = key.InputDesc.Draw.Y,
                Z = key.InputDesc.Draw.Z,
                Ox = key.InputDesc.CenterPoint.X,
                Oy = key.InputDesc.CenterPoint.Y,
                Oz = 0d,
                Sx = zoom.X,
                Sy = zoom.Y,
                Zoom = zoomAvg,
                Aspect = aspect,
                Alpha = key.InputDesc.Opacity * 255d,
                Rx = key.InputDesc.Rotation.X,
                Ry = key.InputDesc.Rotation.Y,
                Rz = key.InputDesc.Rotation.Z,
                Track0 = key.Track0,
                Track1 = key.Track1,
                Track2 = key.Track2,
                Track3 = key.Track3,
                Time = key.Time,
                Frame = key.Frame,
                TotalFrame = key.Length,
                TotalTime = key.Fps > 0 ? key.Length / (double)key.Fps : 0d,
                Framerate = key.Fps,
                TimelineFrame = key.TimelineFrame,
                TimelineTime = key.TimelineTime,
                SceneWidth = key.SceneWidth,
                SceneHeight = key.SceneHeight,
                Layer = key.Layer,
                Index = key.InputIndex,
                Num = key.InputCount,
                GroupIndex = key.GroupIndex,
                GroupCount = key.GroupCount,
                TimelineTotalFrame = key.TimelineTotalFrame,
                TimelineTotalTime = key.TimelineTotalTime,
                IsSaving = key.Usage == TimelineSourceUsage.Exporting,
                IsPlaying = key.Usage == TimelineSourceUsage.Playing,
                IsPaused = key.Usage == TimelineSourceUsage.Paused,
                SceneId = key.SceneId.ToString(),
                TimeRatio = key.Length > 0 ? key.Frame / (double)key.Length : 0d,
            };
        }

        private static DrawDescription BuildOutputDesc(DrawDescription inDesc, AviUtlScriptContext ctx)
        {
            return inDesc with
            {
                Draw = new Vector3((float)ctx.X, (float)ctx.Y, (float)ctx.Z),
                CenterPoint = new Vector2((float)ctx.Ox, (float)ctx.Oy),
                Zoom = new Vector2((float)ctx.Sx, (float)ctx.Sy),
                Opacity = Math.Clamp(ctx.Alpha / 255d, 0d, 1d),
                Rotation = new Vector3((float)ctx.Rx, (float)ctx.Ry, (float)ctx.Rz),
            };
        }

        private byte[] LoadInputPixels(RawRectF bounds, int width, int height)
        {
            EnsureBitmaps(width, height);

            var dc = _ownCtx!.DeviceContext;
            var savedTarget = dc.Target;

            dc.Target = _renderTarget;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(input!, new Vector2(-bounds.Left, -bounds.Top));
            dc.EndDraw();
            dc.Target = savedTarget;

            _stagingBitmap!.CopyFromBitmap(_renderTarget!);

            var mapped = _stagingBitmap.Map(MapOptions.Read);
            try
            {
                if (mapped.Pitch == width * 4)
                {
                    Marshal.Copy(mapped.Bits, _pixelBuffer!, 0, width * height * 4);
                }
                else
                {
                    for (int row = 0; row < height; row++)
                        Marshal.Copy(
                            mapped.Bits + mapped.Pitch * row,
                            _pixelBuffer!,
                            row * width * 4,
                            width * 4);
                }
            }
            finally
            {
                _stagingBitmap.Unmap();
            }

            return _pixelBuffer!;
        }

        private unsafe void WritePixelsToOutput(byte[] pixels, int width)
        {
            fixed (byte* ptr = pixels)
                _outputBitmap!.CopyFromMemory(new nint(ptr), width * 4);
        }

        private void UpdateTransformEffect(RawRectF bounds)
        {
            if (_transformEffect is null)
            {
                _transformEffect = new AffineTransform2D(_ownCtx!.DeviceContext);
                _transformEffect.SetInput(0, _outputBitmap, true);
                _cachedBounds = default;
            }

            if (_cachedBounds.Left != bounds.Left || _cachedBounds.Top != bounds.Top)
            {
                _transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(bounds.Left, bounds.Top);
                _cachedBounds = bounds;
            }
        }

        private void EnsureBitmaps(int width, int height)
        {
            if (_bitmapWidth == width && _bitmapHeight == height) return;

            _renderTarget?.Dispose();
            _stagingBitmap?.Dispose();
            _outputBitmap?.Dispose();
            _transformEffect?.Dispose();
            _renderTarget = null;
            _stagingBitmap = null;
            _outputBitmap = null;
            _transformEffect = null;
            _bitmapWidth = 0;
            _bitmapHeight = 0;

            var dc = _ownCtx!.DeviceContext;

            _renderTarget = dc.CreateEmptyBitmap(width, height, BitmapOptions.Target);

            var stagingProps = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            _stagingBitmap = dc.CreateBitmap(
                new SizeI(width, height),
                nint.Zero,
                width * 4,
                stagingProps);

            _outputBitmap = dc.CreateEmptyBitmap(width, height, BitmapOptions.Target);
            _pixelBuffer = new byte[width * height * 4];

            _bitmapWidth = width;
            _bitmapHeight = height;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _engine.Dispose();
                _renderTarget?.Dispose();
                _stagingBitmap?.Dispose();
                _outputBitmap?.Dispose();
                _transformEffect?.Dispose();
                _renderTarget = null;
                _stagingBitmap = null;
                _outputBitmap = null;
                _transformEffect = null;
            }
            base.Dispose(disposing);
        }
    }
}
