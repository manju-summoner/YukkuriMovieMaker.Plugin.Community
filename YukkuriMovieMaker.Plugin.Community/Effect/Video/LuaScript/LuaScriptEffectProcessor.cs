using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaScriptEffectProcessor(IGraphicsDevicesAndContext devices, LuaScriptEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly LuaScriptEngine _engine = new();

        private GraphicsDevicesAndContext? _ownCtx;

        private ID2D1Bitmap1? _renderTarget;
        private ID2D1Bitmap1? _stagingBitmap;
        private ID2D1Bitmap1? _outputBitmap;
        private byte[]? _pixelBuffer;
        private int _bitmapWidth;
        private int _bitmapHeight;

        private bool _isFirst = true;
        private int _cachedFrame;
        private double _cachedTime;
        private double _cachedTrack0, _cachedTrack1, _cachedTrack2, _cachedTrack3;
        private string _cachedScript = string.Empty;
        private DrawDescription? _cachedInputDesc;
        private DrawDescription? _cachedOutputDesc;
        private bool _cachedPixelsModified;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _ownCtx = new GraphicsDevicesAndContext(devices);
            disposer.Collect(_ownCtx);
            return null;
        }

        protected override void setInput(ID2D1Image? input) { }

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

            if (!_isFirst &&
                frame == _cachedFrame &&
                time == _cachedTime &&
                t0 == _cachedTrack0 &&
                t1 == _cachedTrack1 &&
                t2 == _cachedTrack2 &&
                t3 == _cachedTrack3 &&
                script == _cachedScript &&
                inDesc == _cachedInputDesc)
            {
                effectOutput = _cachedPixelsModified ? _outputBitmap : null;
                return _cachedOutputDesc ?? inDesc;
            }

            var bounds = _ownCtx.DeviceContext.GetImageLocalBounds(input);
            int imgW = Math.Max(1, (int)Math.Ceiling(bounds.Right - bounds.Left));
            int imgH = Math.Max(1, (int)Math.Ceiling(bounds.Bottom - bounds.Top));

            var ctx = BuildContext(desc, inDesc, imgW, imgH, t0, t1, t2, t3, time, frame, length, fps);
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
                    effectOutput = _outputBitmap;
                    pixelsModified = true;
                }
                else
                {
                    effectOutput = null;
                }

                outDesc = BuildOutputDesc(inDesc, ctx);
            }
            catch (Exception ex) when (ex is LuaScriptCompilationException or LuaScriptRuntimeException)
            {
                effectOutput = null;
                outDesc = inDesc;
                Log.Default.Write(ex.Message, ex);
            }

            _isFirst = false;
            _cachedFrame = frame;
            _cachedTime = time;
            _cachedTrack0 = t0;
            _cachedTrack1 = t1;
            _cachedTrack2 = t2;
            _cachedTrack3 = t3;
            _cachedScript = script;
            _cachedInputDesc = inDesc;
            _cachedOutputDesc = outDesc;
            _cachedPixelsModified = pixelsModified;

            return outDesc;
        }

        private static AviUtlScriptContext BuildContext(
            EffectDescription desc, DrawDescription inDesc,
            int imgW, int imgH,
            double t0, double t1, double t2, double t3,
            double time, int frame, int length, int fps)
        {
            double zoomAvg = (inDesc.Zoom.X + inDesc.Zoom.Y) / 2d;
            double aspect = zoomAvg > 0d
                ? (inDesc.Zoom.X - inDesc.Zoom.Y) / (inDesc.Zoom.X + inDesc.Zoom.Y)
                : 0d;

            return new AviUtlScriptContext
            {
                ImageWidth = imgW,
                ImageHeight = imgH,
                X = inDesc.Draw.X,
                Y = inDesc.Draw.Y,
                Z = inDesc.Draw.Z,
                Ox = inDesc.CenterPoint.X,
                Oy = inDesc.CenterPoint.Y,
                Zoom = zoomAvg,
                Aspect = aspect,
                Alpha = inDesc.Opacity * 255d,
                Rx = inDesc.Rotation.X,
                Ry = inDesc.Rotation.Y,
                Rz = inDesc.Rotation.Z,
                Track0 = t0,
                Track1 = t1,
                Track2 = t2,
                Track3 = t3,
                Time = time,
                Frame = frame,
                TotalFrame = length,
                Framerate = fps,
                TimelineFrame = desc.TimelinePosition.Frame,
                TimelineTime = desc.TimelinePosition.Time.TotalSeconds,
                SceneWidth = desc.ScreenSize.Width,
                SceneHeight = desc.ScreenSize.Height,
                Layer = desc.Layer,
            };
        }

        private static DrawDescription BuildOutputDesc(DrawDescription inDesc, AviUtlScriptContext ctx)
        {
            float zoomX = (float)(ctx.Zoom * (1d + ctx.Aspect));
            float zoomY = (float)(ctx.Zoom * (1d - ctx.Aspect));

            return inDesc with
            {
                Draw = new Vector3((float)ctx.X, (float)ctx.Y, (float)ctx.Z),
                CenterPoint = new Vector2((float)ctx.Ox, (float)ctx.Oy),
                Zoom = new Vector2(zoomX, zoomY),
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

        private void EnsureBitmaps(int width, int height)
        {
            if (_bitmapWidth == width && _bitmapHeight == height) return;

            _renderTarget?.Dispose();
            _stagingBitmap?.Dispose();
            _outputBitmap?.Dispose();
            _renderTarget = null;
            _stagingBitmap = null;
            _outputBitmap = null;
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
                _renderTarget = null;
                _stagingBitmap = null;
                _outputBitmap = null;
            }
            base.Dispose(disposing);
        }
    }
}
