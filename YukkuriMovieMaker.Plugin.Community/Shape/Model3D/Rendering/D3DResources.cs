using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal sealed class D3DResources : IDisposable
{
    private const string VertexShaderName = "Model3DVS";
    private const string PixelShaderName = "Model3DPS";
    private const int MaxAnisotropy = 16;

    private readonly DisposeCollector _disposer = new();
    private bool _isDisposed;

    public ID3D11Device Device { get; }
    public ID3D11VertexShader VertexShader { get; }
    public ID3D11PixelShader PixelShader { get; }
    public ID3D11InputLayout InputLayout { get; }
    public ID3D11RasterizerState RasterizerState { get; }
    public ID3D11DepthStencilState DepthWriteState { get; }
    public ID3D11DepthStencilState DepthReadOnlyState { get; }
    public ID3D11SamplerState SamplerState { get; }
    public ID3D11BlendState BlendState { get; }
    public ID3D11ShaderResourceView WhiteTextureView { get; }

    public D3DResources(ID3D11Device device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));

        var vertexShaderBytes = PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(VertexShaderName));
        var pixelShaderBytes = PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(PixelShaderName));

        VertexShader = Collect(device.CreateVertexShader(vertexShaderBytes));
        PixelShader = Collect(device.CreatePixelShader(pixelShaderBytes));
        InputLayout = Collect(CreateInputLayout(device, vertexShaderBytes));
        RasterizerState = Collect(CreateRasterizerState(device));
        DepthWriteState = Collect(CreateDepthStencilState(device, true));
        DepthReadOnlyState = Collect(CreateDepthStencilState(device, false));
        SamplerState = Collect(CreateSamplerState(device));
        BlendState = Collect(CreateBlendState(device));
        WhiteTextureView = Collect(CreateWhiteTexture(device));
    }

    private T Collect<T>(T resource) where T : IDisposable
    {
        _disposer.Collect(resource);
        return resource;
    }

    private static ID3D11InputLayout CreateInputLayout(ID3D11Device device, byte[] vertexShaderBytes)
    {
        InputElementDescription[] elements =
        [
            new("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
            new("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0),
            new("COLOR", 0, Format.R32G32B32A32_Float, 32, 0, InputClassification.PerVertexData, 0)
        ];
        return device.CreateInputLayout(elements, vertexShaderBytes);
    }

    private static ID3D11RasterizerState CreateRasterizerState(ID3D11Device device)
        => device.CreateRasterizerState(new RasterizerDescription(CullMode.None, FillMode.Solid)
        {
            MultisampleEnable = true,
            AntialiasedLineEnable = true
        });

    private static ID3D11DepthStencilState CreateDepthStencilState(ID3D11Device device, bool depthWriteEnabled)
        => device.CreateDepthStencilState(new DepthStencilDescription(
            true,
            depthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
            ComparisonFunction.LessEqual));

    private static ID3D11SamplerState CreateSamplerState(ID3D11Device device)
        => device.CreateSamplerState(new SamplerDescription(
            Filter.Anisotropic,
            TextureAddressMode.Wrap,
            TextureAddressMode.Wrap,
            TextureAddressMode.Wrap,
            0,
            MaxAnisotropy,
            ComparisonFunction.Always,
            new Color4(0, 0, 0, 0),
            0,
            float.MaxValue));

    private static ID3D11BlendState CreateBlendState(ID3D11Device device)
    {
        var description = new BlendDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = false
        };

        description.RenderTarget[0] = new RenderTargetBlendDescription
        {
            IsBlendEnabled = true,
            SourceBlend = Blend.One,
            DestinationBlend = Blend.InverseSourceAlpha,
            BlendOperation = BlendOperation.Add,
            SourceBlendAlpha = Blend.One,
            DestinationBlendAlpha = Blend.InverseSourceAlpha,
            BlendOperationAlpha = BlendOperation.Add,
            RenderTargetWriteMask = ColorWriteEnable.All
        };

        return device.CreateBlendState(description);
    }

    private static unsafe ID3D11ShaderResourceView CreateWhiteTexture(ID3D11Device device)
    {
        var whitePixel = new byte[] { 255, 255, 255, 255 };
        var description = new Texture2DDescription
        {
            Width = 1,
            Height = 1,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource
        };

        fixed (byte* pointer = whitePixel)
        {
            using var texture = device.CreateTexture2D(description, [new SubresourceData(pointer, 4)]);
            return device.CreateShaderResourceView(texture);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _disposer.Dispose();
    }
}
