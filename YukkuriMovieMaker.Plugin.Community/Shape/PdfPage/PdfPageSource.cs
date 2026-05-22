using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using WinRtPdf = Windows.Data.Pdf;
using WinRtBuffer = Windows.Storage.Streams.Buffer;
using Windows.Storage;
using Windows.Storage.Streams;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Shape.PdfPage;

internal sealed class PdfPageSource(IGraphicsDevicesAndContext devices, PdfPageParameter parameter)
    : IShapeSource
{
    private ID2D1CommandList _commandList = CreateEmptyCommandList(devices.DeviceContext);
    private bool _disposedValue;

    private string _file = string.Empty;
    private int _pageIndex = -1;
    private double _scale = double.NaN;
    private double _renderDpi = double.NaN;

    private WinRtPdf.PdfDocument? _pdfDocument;
    private string _loadedFile = string.Empty;

    private ID2D1Bitmap? _cachedBitmap;
    private double _cachedBitmapDpi = double.NaN;

    public ID2D1Image Output => _commandList;

    public void Update(TimelineItemSourceDescription desc)
    {
        var fps = desc.FPS;
        var frame = desc.ItemPosition.Frame;
        var length = desc.ItemDuration.Frame;

        var file = parameter.File;
        var pageIndex = (int)parameter.PageNumber.GetValue(frame, length, fps) - 1;
        var scale = parameter.Scale.GetValue(frame, length, fps);
        var renderDpi = parameter.RenderDpi.GetValue(frame, length, fps);

        if (_file == file &&
            _pageIndex == pageIndex &&
            _scale == scale &&
            _renderDpi == renderDpi)
            return;

        var bitmapInputsChanged =
            _file != file ||
            _pageIndex != pageIndex ||
            _renderDpi != renderDpi;

        _file = file;
        _pageIndex = pageIndex;
        _scale = scale;
        _renderDpi = renderDpi;

        if (bitmapInputsChanged)
            RefreshBitmap(file, pageIndex, renderDpi);

        var newCommandList = BuildCommandList(_cachedBitmap, scale);

        var old = _commandList;
        _commandList = newCommandList;
        old.Dispose();
    }

    private void RefreshBitmap(string file, int pageIndex, double renderDpi)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
        {
            DisposeCachedBitmap();
            return;
        }

        EnsurePdfDocument(file);

        if (_pdfDocument is null)
        {
            DisposeCachedBitmap();
            return;
        }

        var pageCount = (int)_pdfDocument.PageCount;
        if (pageCount <= 0 || pageIndex < 0 || pageIndex >= pageCount)
        {
            DisposeCachedBitmap();
            return;
        }

        using var page = _pdfDocument.GetPage((uint)pageIndex);
        var dpiScale = (float)(renderDpi / 96.0);
        var (renderWidth, renderHeight) = ComputeRenderSize(page.Size.Width, page.Size.Height, dpiScale);

        var bitmap = RasterizePage(page, renderWidth, renderHeight);

        DisposeCachedBitmap();
        _cachedBitmap = bitmap;
        _cachedBitmapDpi = renderDpi;
    }

    private ID2D1CommandList BuildCommandList(ID2D1Bitmap? bitmap, double scale)
    {
        if (bitmap is null)
            return CreateEmptyCommandList(devices.DeviceContext);

        return CreateCenteredBitmapCommandList(devices.DeviceContext, bitmap, scale, _cachedBitmapDpi);
    }

    private static ID2D1CommandList CreateEmptyCommandList(ID2D1DeviceContext dc)
    {
        var commandList = dc.CreateCommandList();
        dc.Target = commandList;
        dc.BeginDraw();
        dc.Clear(null);

        //1pxの透明を描画する
        //これがないと描画内容が空の場合にcommandListの画面サイズが定まらず、エラーになる
        using (var transparent = dc.CreateSolidColorBrush(new Vortice.Mathematics.Color4(0, 0, 0, 0)))
            dc.DrawRectangle(new Vortice.RawRectF(0, 0, 1, 1), transparent);

        dc.EndDraw();
        dc.Target = null;
        commandList.Close();
        return commandList;
    }

    private static ID2D1CommandList CreateCenteredBitmapCommandList(
        ID2D1DeviceContext dc,
        ID2D1Bitmap bitmap,
        double scale,
        double bitmapDpi)
    {
        var size = bitmap.Size;
        var displayScale = (float)(scale / 100.0 * 96.0 / bitmapDpi);
        var displayWidth = size.Width * displayScale;
        var displayHeight = size.Height * displayScale;

        var commandList = dc.CreateCommandList();
        dc.Target = commandList;
        dc.BeginDraw();
        dc.Clear(null);
        dc.Transform =
            Matrix3x2.CreateScale(displayScale) *
            Matrix3x2.CreateTranslation(-displayWidth / 2f, -displayHeight / 2f);
        dc.DrawBitmap(bitmap, 1.0f, InterpolationMode.HighQualityCubic);
        dc.Transform = Matrix3x2.Identity;
        dc.EndDraw();
        dc.Target = null;
        commandList.Close();
        return commandList;
    }

    private void EnsurePdfDocument(string file)
    {
        if (_loadedFile == file && _pdfDocument is not null)
            return;

        _pdfDocument = null;
        _loadedFile = string.Empty;

        try
        {
            var storageFile = StorageFile.GetFileFromPathAsync(Path.GetFullPath(file))
                .AsTask().GetAwaiter().GetResult();
            _pdfDocument = WinRtPdf.PdfDocument.LoadFromFileAsync(storageFile)
                .AsTask().GetAwaiter().GetResult();
            _loadedFile = file;
        }
        catch
        {
            _pdfDocument = null;
        }
    }

    private static (int width, int height) ComputeRenderSize(
        double pageWidth,
        double pageHeight,
        float dpiScale)
    {
        return (Math.Max(1, (int)Math.Round(pageWidth * dpiScale)),
                Math.Max(1, (int)Math.Round(pageHeight * dpiScale)));
    }

    private ID2D1Bitmap? RasterizePage(WinRtPdf.PdfPage page, int renderWidth, int renderHeight)
    {
        using var stream = new InMemoryRandomAccessStream();

        var options = new WinRtPdf.PdfPageRenderOptions
        {
            DestinationWidth = (uint)renderWidth,
            DestinationHeight = (uint)renderHeight,
        };

        page.RenderToStreamAsync(stream, options).AsTask().GetAwaiter().GetResult();

        stream.Seek(0);
        var buffer = new WinRtBuffer((uint)stream.Size);
        var readBuffer = stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None)
            .AsTask().GetAwaiter().GetResult();
        var bytes = readBuffer.ToArray(0, (int)readBuffer.Length);

        using var memStream = new MemoryStream(bytes);
        using var wicFactory = new Vortice.WIC.IWICImagingFactory();
        using var decoder = wicFactory.CreateDecoderFromStream(
            memStream, Vortice.WIC.DecodeOptions.CacheOnLoad);
        using var frame = decoder.GetFrame(0);
        using var converter = wicFactory.CreateFormatConverter();
        converter.Initialize(
            frame,
            Vortice.WIC.PixelFormat.Format32bppPBGRA,
            Vortice.WIC.BitmapDitherType.None,
            null,
            0,
            Vortice.WIC.BitmapPaletteType.MedianCut);

        return devices.DeviceContext.CreateBitmapFromWicBitmap(converter);
    }

    private void DisposeCachedBitmap()
    {
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
        _cachedBitmapDpi = double.NaN;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PdfPageSource() => Dispose(false);

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _commandList.Dispose();
            DisposeCachedBitmap();
        }
        _disposedValue = true;
    }
}
