using System.IO;
using System.Runtime.CompilerServices;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal sealed class GpuResourceFactory(ITextureService textureService)
{
    public unsafe GpuResourceCacheItem? Create(ID3D11Device device, Model3DData model)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (model.Vertices.Length == 0 || model.Indices.Length == 0) return null;

        ID3D11Buffer? vertexBuffer = null;
        ID3D11Buffer? indexBuffer = null;
        ID3D11ShaderResourceView?[]? textures = null;
        ID3D11ShaderResourceView?[]? metallicRoughnessTextures = null;
        bool success = false;

        try
        {
            long gpuBytes = 0;

            int vertexBufferSize = model.Vertices.Length * Unsafe.SizeOf<Model3DVertex>();
            fixed (Model3DVertex* pVertices = model.Vertices)
            {
                vertexBuffer = device.CreateBuffer(
                    new BufferDescription(vertexBufferSize, BindFlags.VertexBuffer, ResourceUsage.Immutable, CpuAccessFlags.None),
                    new SubresourceData(pVertices));
            }
            gpuBytes += vertexBufferSize;

            int indexBufferSize = model.Indices.Length * sizeof(int);
            fixed (int* pIndices = model.Indices)
            {
                indexBuffer = device.CreateBuffer(
                    new BufferDescription(indexBufferSize, BindFlags.IndexBuffer, ResourceUsage.Immutable, CpuAccessFlags.None),
                    new SubresourceData(pIndices));
            }
            gpuBytes += indexBufferSize;

            var parts = BoundingBoxUtility.CalculatePartCenters(model);
            textures = new ID3D11ShaderResourceView?[parts.Length];
            metallicRoughnessTextures = new ID3D11ShaderResourceView?[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                gpuBytes += LoadTexture(device, parts[i].TexturePath, textures, i);
                gpuBytes += LoadTexture(device, parts[i].MetallicRoughnessTexturePath, metallicRoughnessTextures, i);
            }

            if (!Model3DSettings.Default.IsGpuMemoryPerModelAllowed(gpuBytes)) return null;

            int opaquePartCount = PartitionOpaqueFirst(parts, textures, metallicRoughnessTextures);

            var item = new GpuResourceCacheItem(
                vertexBuffer,
                indexBuffer,
                model.Indices.Length,
                parts,
                textures,
                metallicRoughnessTextures,
                model.ModelCenter,
                model.ModelScale,
                opaquePartCount);

            success = true;
            return item;
        }
        finally
        {
            if (!success)
            {
                DisposeTextures(textures);
                DisposeTextures(metallicRoughnessTextures);
                SafeDispose(indexBuffer);
                SafeDispose(vertexBuffer);
            }
        }
    }

    private long LoadTexture(ID3D11Device device, string texturePath, ID3D11ShaderResourceView?[] target, int index)
    {
        if (string.IsNullOrEmpty(texturePath) || !File.Exists(texturePath)) return 0;

        try
        {
            var (view, textureBytes) = textureService.CreateShaderResourceView(texturePath, device);
            target[index] = view;
            return textureBytes;
        }
        catch
        {
            return 0;
        }
    }

    private static int PartitionOpaqueFirst(
        Model3DPart[] parts,
        ID3D11ShaderResourceView?[] textures,
        ID3D11ShaderResourceView?[] metallicRoughnessTextures)
    {
        var opaque = new List<(Model3DPart Part, ID3D11ShaderResourceView? Texture, ID3D11ShaderResourceView? MetallicRoughness)>(parts.Length);
        var transparent = new List<(Model3DPart Part, ID3D11ShaderResourceView? Texture, ID3D11ShaderResourceView? MetallicRoughness)>(parts.Length);

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].IsOpaque) opaque.Add((parts[i], textures[i], metallicRoughnessTextures[i]));
            else transparent.Add((parts[i], textures[i], metallicRoughnessTextures[i]));
        }

        int index = 0;
        foreach (var (part, texture, metallicRoughness) in opaque)
        {
            parts[index] = part;
            textures[index] = texture;
            metallicRoughnessTextures[index] = metallicRoughness;
            index++;
        }

        foreach (var (part, texture, metallicRoughness) in transparent)
        {
            parts[index] = part;
            textures[index] = texture;
            metallicRoughnessTextures[index] = metallicRoughness;
            index++;
        }

        return opaque.Count;
    }

    private static void DisposeTextures(ID3D11ShaderResourceView?[]? textures)
    {
        if (textures is null) return;

        for (int i = 0; i < textures.Length; i++)
        {
            SafeDispose(textures[i]);
        }
    }

    private static void SafeDispose(IDisposable? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch
        {
        }
    }
}
