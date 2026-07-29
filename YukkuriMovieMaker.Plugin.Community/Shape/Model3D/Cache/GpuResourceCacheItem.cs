using System.Numerics;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;
using static YukkuriMovieMaker.Plugin.Community.Shape.Model3D.DisposeUtility;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal sealed class GpuResourceCacheItem : IDisposable
{
    private int _disposed;
    private ID3D11ShaderResourceView?[]? _partTextures;
    private ID3D11ShaderResourceView?[]? _partMetallicRoughnessTextures;

    public ID3D11Buffer VertexBuffer { get; }
    public ID3D11Buffer IndexBuffer { get; }
    public Model3DPart[] Parts { get; }
    public ID3D11ShaderResourceView?[] PartTextures => _partTextures!;
    public ID3D11ShaderResourceView?[] PartMetallicRoughnessTextures => _partMetallicRoughnessTextures!;
    public Vector3 ModelCenter { get; }
    public float ModelScale { get; }
    public int OpaquePartCount { get; }

    public GpuResourceCacheItem(
        ID3D11Buffer vertexBuffer,
        ID3D11Buffer indexBuffer,
        Model3DPart[] parts,
        ID3D11ShaderResourceView?[] textures,
        ID3D11ShaderResourceView?[] metallicRoughnessTextures,
        Vector3 modelCenter,
        float modelScale,
        int opaquePartCount)
    {
        VertexBuffer = vertexBuffer ?? throw new ArgumentNullException(nameof(vertexBuffer));
        IndexBuffer = indexBuffer ?? throw new ArgumentNullException(nameof(indexBuffer));
        Parts = parts ?? throw new ArgumentNullException(nameof(parts));
        _partTextures = textures ?? throw new ArgumentNullException(nameof(textures));
        _partMetallicRoughnessTextures = metallicRoughnessTextures ?? throw new ArgumentNullException(nameof(metallicRoughnessTextures));
        ModelCenter = modelCenter;
        ModelScale = modelScale;
        OpaquePartCount = opaquePartCount;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        SafeDispose(VertexBuffer);
        SafeDispose(IndexBuffer);

        DisposeTextures(ref _partTextures);
        DisposeTextures(ref _partMetallicRoughnessTextures);
    }

    private static void DisposeTextures(ref ID3D11ShaderResourceView?[]? textures)
    {
        var local = textures;
        textures = null;
        if (local is null) return;

        for (int i = 0; i < local.Length; i++)
        {
            SafeDispose(local[i]);
            local[i] = null;
        }
    }
}
