using System.IO;
using System.Runtime.CompilerServices;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;
using static YukkuriMovieMaker.Plugin.Community.Shape.Model3D.DisposeUtility;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal sealed class GpuResourceFactory(ITextureService textureService)
{
    public unsafe GpuResourceCacheItem? Create(ID3D11Device device, Model3DData model, out bool textureLoadFailed)
    {
        textureLoadFailed = false;
        ArgumentNullException.ThrowIfNull(device);
        if (model.Vertices.Length == 0 || model.Indices.Length == 0) return null;

        foreach (var part in model.Parts)
        {
            if (part.IndexOffset < 0 || part.IndexCount < 0
                || (long)part.IndexOffset + part.IndexCount > model.Indices.Length) return null;
        }

        foreach (var index in model.Indices)
        {
            if ((uint)index >= (uint)model.Vertices.Length) return null;
        }

        long vertexBytes = (long)model.Vertices.Length * Unsafe.SizeOf<Model3DVertex>();
        long indexBytes = (long)model.Indices.Length * sizeof(int);
        if (vertexBytes > int.MaxValue || indexBytes > int.MaxValue) return null;
        if (!Model3DSettings.Default.IsGpuMemoryPerModelAllowed(vertexBytes + indexBytes)) return null;

        ID3D11Buffer? vertexBuffer = null;
        ID3D11Buffer? indexBuffer = null;
        ID3D11ShaderResourceView?[]? textures = null;
        ID3D11ShaderResourceView?[]? metallicRoughnessTextures = null;
        bool success = false;

        try
        {
            long gpuBytes = 0;

            int vertexBufferSize = (int)vertexBytes;
            fixed (Model3DVertex* pVertices = model.Vertices)
            {
                vertexBuffer = device.CreateBuffer(
                    new BufferDescription(vertexBufferSize, BindFlags.VertexBuffer, ResourceUsage.Immutable, CpuAccessFlags.None),
                    new SubresourceData(pVertices));
            }
            gpuBytes += vertexBufferSize;

            int indexBufferSize = (int)indexBytes;
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

            var cachedTexturePaths = new List<string>();

            bool anyTextureFailed = false;
            for (int i = 0; i < parts.Length; i++)
            {
                gpuBytes += LoadTexture(device, parts[i].TexturePath, textures, i, cachedTexturePaths, ref anyTextureFailed);
                gpuBytes += LoadTexture(device, parts[i].MetallicRoughnessTexturePath, metallicRoughnessTextures, i, cachedTexturePaths, ref anyTextureFailed);
                if (!Model3DSettings.Default.IsGpuMemoryPerModelAllowed(gpuBytes)) break;
            }
            textureLoadFailed = anyTextureFailed;

            if (!Model3DSettings.Default.IsGpuMemoryPerModelAllowed(gpuBytes))
            {
                foreach (var path in cachedTexturePaths)
                    textureService.EvictGpuTexture(path, device);
                return null;
            }

            int opaquePartCount = PartitionOpaqueFirst(parts, textures, metallicRoughnessTextures);

            var item = new GpuResourceCacheItem(
                vertexBuffer,
                indexBuffer,
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

    private long LoadTexture(ID3D11Device device, string texturePath, ID3D11ShaderResourceView?[] target, int index, List<string> cachedTexturePaths, ref bool anyTextureFailed)
    {
        if (string.IsNullOrEmpty(texturePath) || !File.Exists(texturePath)) return 0;

        try
        {
            var (view, textureBytes) = textureService.CreateShaderResourceView(texturePath, device);
            target[index] = view;
            if (view is null) anyTextureFailed = true;
            if (textureBytes > 0) cachedTexturePaths.Add(texturePath);
            return textureBytes;
        }
        catch
        {
            anyTextureFailed = true;
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
}
