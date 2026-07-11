using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering.Buffers;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal sealed class Model3DRenderer : IDisposable
{
    private static readonly Color4 TransparentBlack = new(0, 0, 0, 0);
    private const float DegreesToRadians = MathF.PI / 180.0f;

    private readonly D3DResources _resources;
    private readonly ConstantBuffer<CBPerFrame> _perFrame;
    private readonly ConstantBuffer<CBPerObject> _perObject;
    private readonly ConstantBuffer<CBPerMaterial> _perMaterial;

    private readonly ID3D11Buffer[] _vertexBufferBinding = new ID3D11Buffer[1];
    private readonly int[] _vertexStride = [Unsafe.SizeOf<Model3DVertex>()];
    private readonly int[] _vertexOffset = [0];
    private readonly ID3D11ShaderResourceView[] _textureBinding = new ID3D11ShaderResourceView[2];
    private readonly ID3D11SamplerState[] _samplerBinding;
    private readonly ID3D11Buffer[] _perFrameBinding;
    private readonly ID3D11Buffer[] _perObjectBinding;
    private readonly ID3D11Buffer[] _perMaterialBinding;
    private readonly ID3D11RenderTargetView[] _emptyRenderTargets = new ID3D11RenderTargetView[1];
    private readonly ID3D11ShaderResourceView[] _emptyTextures = new ID3D11ShaderResourceView[2];

    private int[] _transparentOrder = [];
    private float[] _transparentDepth = [];
    private bool _disposed;

    public Model3DRenderer(D3DResources resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));

        _perFrame = new ConstantBuffer<CBPerFrame>(resources.Device);
        _perObject = new ConstantBuffer<CBPerObject>(resources.Device);
        _perMaterial = new ConstantBuffer<CBPerMaterial>(resources.Device);

        _samplerBinding = [resources.SamplerState];
        _perFrameBinding = [_perFrame.Buffer];
        _perObjectBinding = [_perObject.Buffer];
        _perMaterialBinding = [_perMaterial.Buffer];
    }

    public void Render(
        ID3D11DeviceContext context,
        RenderTargetManager targets,
        GpuResourceCacheItem model,
        int width,
        int height,
        in Model3DRenderState state)
    {
        if (_disposed) return;
        if (targets.RenderTargetView is not { } renderTargetView) return;
        if (targets.DepthStencilView is not { } depthStencilView) return;

        var world = CreateWorldMatrix(model.ModelCenter, model.ModelScale, state);
        var (view, projection, cameraPosition) = CreateCamera(state, width, height);

        context.OMSetRenderTargets(renderTargetView, depthStencilView);
        context.ClearRenderTargetView(renderTargetView, TransparentBlack);
        context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

        context.RSSetViewport(0, 0, width, height);
        context.RSSetState(_resources.RasterizerState);
        context.OMSetBlendState(_resources.BlendState, TransparentBlack, -1);

        context.IASetInputLayout(_resources.InputLayout);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _vertexBufferBinding[0] = model.VertexBuffer;
        context.IASetVertexBuffers(0, 1, _vertexBufferBinding, _vertexStride, _vertexOffset);
        context.IASetIndexBuffer(model.IndexBuffer, Format.R32_UInt, 0);

        context.VSSetShader(_resources.VertexShader);
        context.PSSetShader(_resources.PixelShader);
        context.PSSetSamplers(0, 1, _samplerBinding);

        var perFrame = new CBPerFrame
        {
            CameraPosition = new Vector4(cameraPosition, 1.0f),
            LightPosition = new Vector4(ToWorldSpace(state.LightPosition), 1.0f),
            LightTarget = new Vector4(ToWorldSpace(state.Position), 1.0f),
            LightType = (float)state.LightType,
            LightEnabled = state.IsLightEnabled ? 1.0f : 0.0f
        };
        _perFrame.Update(context, ref perFrame);
        context.PSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, _perFrameBinding);

        var perObject = new CBPerObject
        {
            WorldViewProjection = Matrix4x4.Transpose(world * view * projection),
            World = Matrix4x4.Transpose(world)
        };
        _perObject.Update(context, ref perObject);
        context.VSSetConstantBuffers(RenderingConstants.CbSlotPerObject, 1, _perObjectBinding);

        int opaquePartCount = state.BaseColor.W >= 1.0f ? model.OpaquePartCount : 0;

        context.OMSetDepthStencilState(_resources.DepthWriteState);
        for (int i = 0; i < opaquePartCount; i++)
            DrawPart(context, model, i, state.BaseColor, true);

        context.OMSetDepthStencilState(_resources.DepthReadOnlyState);
        foreach (int index in OrderTransparentPartsBackToFront(model, world * view, opaquePartCount))
            DrawPart(context, model, index, state.BaseColor, false);

        UnbindResources(context);
    }

    private void DrawPart(ID3D11DeviceContext context, GpuResourceCacheItem model, int index, Vector4 uiBaseColor, bool isOpaquePass)
    {
        var part = model.Parts[index];
        if (part.IndexCount <= 0) return;

        _textureBinding[0] = model.PartTextures[index] ?? _resources.WhiteTextureView;
        _textureBinding[1] = model.PartMetallicRoughnessTextures[index] ?? _resources.WhiteTextureView;
        context.PSSetShaderResources(RenderingConstants.SlotBaseColorTexture, 2, _textureBinding);

        _samplerBinding[0] = _resources.GetSampler(part.AddressU, part.AddressV);
        context.PSSetSamplers(0, 1, _samplerBinding);

        var perMaterial = new CBPerMaterial
        {
            BaseColor = part.BaseColor * uiBaseColor,
            Metallic = part.Metallic,
            Roughness = part.Roughness,
            AlphaCutoff = part.AlphaCutoff,
            ForceOpaque = isOpaquePass && part.IgnoreAlpha && uiBaseColor.W >= 1.0f ? 1.0f : 0.0f
        };
        _perMaterial.Update(context, ref perMaterial);
        context.PSSetConstantBuffers(RenderingConstants.CbSlotPerMaterial, 1, _perMaterialBinding);

        context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
    }

    private Span<int> OrderTransparentPartsBackToFront(GpuResourceCacheItem model, Matrix4x4 worldView, int firstPartIndex)
    {
        int count = model.Parts.Length - firstPartIndex;
        if (count <= 0) return [];

        if (_transparentOrder.Length < count)
        {
            _transparentOrder = new int[count];
            _transparentDepth = new float[count];
        }

        for (int i = 0; i < count; i++)
        {
            int index = firstPartIndex + i;
            _transparentOrder[i] = index;
            _transparentDepth[i] = Vector3.Transform(model.Parts[index].Center, worldView).Z;
        }

        Array.Sort(_transparentDepth, _transparentOrder, 0, count);
        return _transparentOrder.AsSpan(0, count);
    }

    internal static Matrix4x4 CreateWorldMatrix(Vector3 modelCenter, float modelScale, in Model3DRenderState state)
    {
        float pixelScale = modelScale * RenderingConstants.DefaultModelSize / Model3DData.NormalizedSize;
        var normalize = Matrix4x4.CreateTranslation(-modelCenter) * Matrix4x4.CreateScale(pixelScale);

        var rotation = Matrix4x4.CreateRotationX(state.Rotation.X * DegreesToRadians)
                     * Matrix4x4.CreateRotationY(state.Rotation.Y * DegreesToRadians)
                     * Matrix4x4.CreateRotationZ(state.Rotation.Z * DegreesToRadians);

        var transform = rotation
                      * Matrix4x4.CreateScale(state.Scale)
                      * Matrix4x4.CreateTranslation(ToWorldSpace(state.Position));

        return normalize * transform;
    }

    internal static Vector3 ToWorldSpace(Vector3 screenSpacePosition)
        => new(-screenSpacePosition.X, -screenSpacePosition.Y, screenSpacePosition.Z);

    internal static (Matrix4x4 View, Matrix4x4 Projection, Vector3 CameraPosition) CreateCamera(in Model3DRenderState state, int width, int height)
    {
        float fieldOfView = Math.Clamp(state.FieldOfView, RenderingConstants.MinFieldOfView, RenderingConstants.MaxFieldOfView) * DegreesToRadians;
        float distance = height / (2.0f * MathF.Tan(fieldOfView * 0.5f));
        float nearPlane = distance * RenderingConstants.NearPlaneRatio;
        float farPlane = distance * RenderingConstants.FarPlaneRatio;

        var cameraPosition = new Vector3(0.0f, 0.0f, -distance);
        var view = Matrix4x4.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.UnitY);

        var projection = state.Projection == ProjectionType.Parallel
            ? Matrix4x4.CreateOrthographic(width, height, nearPlane, farPlane)
            : Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, (float)width / height, nearPlane, farPlane);

        return (view, projection, cameraPosition);
    }

    private void UnbindResources(ID3D11DeviceContext context)
    {
        context.PSSetShaderResources(RenderingConstants.SlotBaseColorTexture, 2, _emptyTextures);
        context.OMSetRenderTargets(0, _emptyRenderTargets, null);
        context.RSSetState(null);
        context.OMSetDepthStencilState(null);
        context.OMSetBlendState(null, TransparentBlack, -1);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _perMaterial.Dispose();
        _perObject.Dispose();
        _perFrame.Dispose();
    }
}
