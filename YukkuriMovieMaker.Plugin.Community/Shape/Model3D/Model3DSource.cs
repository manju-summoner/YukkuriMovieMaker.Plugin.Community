using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal sealed class Model3DSource : IShapeSource
{
    private static readonly Lock RenderLock = new();

    private readonly IGraphicsDevicesAndContext _devices;
    private readonly Model3DParameter _parameter;
    private readonly TextureService _textureService = new();
    private readonly RenderTargetManager _renderTargets = new();
    private readonly GpuResourceFactory _gpuResourceFactory;
    private readonly D3DResources _resources;
    private readonly Model3DRenderer _renderer;

    private ID2D1CommandList _commandList;
    private GpuResourceCacheItem? _gpuResource;
    private string _file = string.Empty;
    private string _gpuResourceKey = string.Empty;
    private int _width;
    private int _height;
    private Model3DRenderState _state;
    private bool _disposed;

    public Model3DSource(IGraphicsDevicesAndContext devices, Model3DParameter parameter)
    {
        _devices = devices;
        _parameter = parameter;
        _resources = new D3DResources(devices.D3D.Device);
        _renderer = new Model3DRenderer(_resources);
        _gpuResourceFactory = new GpuResourceFactory(_textureService);
        _commandList = CreateEmptyCommandList(devices.DeviceContext);
    }

    public ID2D1Image Output => _commandList;

    public void Update(TimelineItemSourceDescription desc)
    {
        if (_disposed) return;

        var fps = desc.FPS;
        var frame = desc.ItemPosition.Frame;
        var length = desc.ItemDuration.Frame;

        var file = _parameter.File;
        int width = ClampRenderSize(desc.ScreenSize.Width);
        int height = ClampRenderSize(desc.ScreenSize.Height);
        var state = CreateRenderState(frame, length, fps);

        if (_file == file && _width == width && _height == height && _state == state) return;

        _file = file;
        _width = width;
        _height = height;
        _state = state;

        ID2D1CommandList newCommandList;
        try
        {
            newCommandList = BuildCommandList(file, width, height, state);
        }
        catch
        {
            return;
        }

        var oldCommandList = _commandList;
        _commandList = newCommandList;
        oldCommandList.Dispose();
    }

    private Model3DRenderState CreateRenderState(int frame, int length, int fps)
    {
        var baseColor = _parameter.BaseColor;

        return new Model3DRenderState(
            Position: new Vector3(
                (float)_parameter.X.GetValue(frame, length, fps),
                (float)_parameter.Y.GetValue(frame, length, fps),
                (float)_parameter.Z.GetValue(frame, length, fps)),
            Rotation: new Vector3(
                (float)_parameter.RotationX.GetValue(frame, length, fps),
                (float)_parameter.RotationY.GetValue(frame, length, fps),
                (float)_parameter.RotationZ.GetValue(frame, length, fps)),
            Scale: (float)(_parameter.Scale.GetValue(frame, length, fps) / 100.0),
            FieldOfView: (float)_parameter.Fov.GetValue(frame, length, fps),
            Projection: _parameter.Projection,
            BaseColor: new Vector4(baseColor.ScR, baseColor.ScG, baseColor.ScB, baseColor.ScA),
            LightPosition: new Vector3(
                (float)_parameter.LightX.GetValue(frame, length, fps),
                (float)_parameter.LightY.GetValue(frame, length, fps),
                (float)_parameter.LightZ.GetValue(frame, length, fps)),
            LightType: _parameter.LightType,
            IsLightEnabled: _parameter.IsLightEnabled);
    }

    private ID2D1CommandList BuildCommandList(string file, int width, int height, in Model3DRenderState state)
    {
        var deviceContext = _devices.DeviceContext;

        var resource = AcquireGpuResource(file);
        if (resource is null) return CreateEmptyCommandList(deviceContext);

        lock (RenderLock)
        {
            if (!_renderTargets.EnsureSize(_devices, width, height))
                return CreateEmptyCommandList(deviceContext);

            var context = _devices.D3D.DeviceContext;
            _renderer.Render(context, _renderTargets, resource, width, height, state);
            context.Flush();
        }

        return _renderTargets.SharedBitmap is { } bitmap
            ? CreateCenteredBitmapCommandList(deviceContext, bitmap)
            : CreateEmptyCommandList(deviceContext);
    }

    private GpuResourceCacheItem? AcquireGpuResource(string file)
    {
        string key = file ?? string.Empty;
        if (_gpuResourceKey == key) return _gpuResource;

        _gpuResource?.Dispose();
        _gpuResource = null;
        _gpuResourceKey = key;

        if (key.Length == 0) return null;

        try
        {
            var model = Model3DLoader.Load(key);
            if (model.Vertices.Length == 0) return null;

            _gpuResource = _gpuResourceFactory.Create(_devices.D3D.Device, model);
        }
        catch
        {
            _gpuResource = null;
        }

        return _gpuResource;
    }

    private static int ClampRenderSize(int size)
        => Math.Clamp(size, RenderingConstants.MinRenderSize, RenderingConstants.MaxRenderSize);

    private static ID2D1CommandList CreateEmptyCommandList(ID2D1DeviceContext deviceContext)
    {
        var commandList = deviceContext.CreateCommandList();
        try
        {
            deviceContext.Target = commandList;
            deviceContext.BeginDraw();
            try
            {
                deviceContext.Clear(null);

                using var transparent = deviceContext.CreateSolidColorBrush(new Vortice.Mathematics.Color4(0, 0, 0, 0));
                deviceContext.DrawRectangle(new Vortice.RawRectF(0, 0, 1, 1), transparent);
            }
            finally
            {
                try
                {
                    deviceContext.EndDraw();
                }
                finally
                {
                    deviceContext.Target = null;
                }
            }

            commandList.Close();
            return commandList;
        }
        catch
        {
            commandList.Dispose();
            throw;
        }
    }

    private static ID2D1CommandList CreateCenteredBitmapCommandList(ID2D1DeviceContext deviceContext, ID2D1Bitmap1 bitmap)
    {
        var size = bitmap.Size;

        var commandList = deviceContext.CreateCommandList();
        try
        {
            deviceContext.Target = commandList;
            deviceContext.BeginDraw();
            try
            {
                deviceContext.Clear(null);
                deviceContext.Transform = Matrix3x2.CreateTranslation(-size.Width / 2f, -size.Height / 2f);
                deviceContext.DrawImage(bitmap);
            }
            finally
            {
                deviceContext.Transform = Matrix3x2.Identity;
                try
                {
                    deviceContext.EndDraw();
                }
                finally
                {
                    deviceContext.Target = null;
                }
            }

            commandList.Close();
            return commandList;
        }
        catch
        {
            commandList.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gpuResource?.Dispose();
        _commandList.Dispose();
        _renderer.Dispose();
        _renderTargets.Dispose();
        _textureService.Dispose();
        _resources.Dispose();
    }
}
