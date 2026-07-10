using System.Numerics;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal sealed class GpuResourceCacheItem : IDisposable
{
    private int _disposed;
    private ID3D11ShaderResourceView?[]? _partTextures;

    public ID3D11Buffer VertexBuffer { get; }
    public ID3D11Buffer IndexBuffer { get; }
    public int IndexCount { get; }
    public Model3DPart[] Parts { get; }
    public ID3D11ShaderResourceView?[] PartTextures => _partTextures!;
    public Vector3 ModelCenter { get; }
    public float ModelScale { get; }
    public int OpaquePartCount { get; }

    public GpuResourceCacheItem(
        ID3D11Buffer vertexBuffer,
        ID3D11Buffer indexBuffer,
        int indexCount,
        Model3DPart[] parts,
        ID3D11ShaderResourceView?[] textures,
        Vector3 modelCenter,
        float modelScale,
        int opaquePartCount)
    {
        VertexBuffer = vertexBuffer ?? throw new ArgumentNullException(nameof(vertexBuffer));
        IndexBuffer = indexBuffer ?? throw new ArgumentNullException(nameof(indexBuffer));
        IndexCount = indexCount;
        Parts = parts ?? throw new ArgumentNullException(nameof(parts));
        _partTextures = textures ?? throw new ArgumentNullException(nameof(textures));
        ModelCenter = modelCenter;
        ModelScale = modelScale;
        OpaquePartCount = opaquePartCount;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        SafeDispose(VertexBuffer);
        SafeDispose(IndexBuffer);

        var textures = _partTextures;
        _partTextures = null;
        if (textures is null) return;

        for (int i = 0; i < textures.Length; i++)
        {
            SafeDispose(textures[i]);
            textures[i] = null;
        }
    }

    private static void SafeDispose(IDisposable? disposable)
    {
        if (disposable is null) return;

        try
        {
            disposable.Dispose();
        }
        catch
        {
        }
    }
}
