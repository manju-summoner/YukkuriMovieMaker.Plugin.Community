using System.Numerics;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal delegate void CacheChunkWriter(ReadOnlySpan<byte> data);

internal interface IStreamingCacheWriter : IDisposable
{
    void WriteHeader(CacheHeader header);
    void WriteMetadata(int vertexCount, int indexCount, List<Model3DPart> parts, Vector3 center, float scale);
    void WriteVertexChunk(ReadOnlySpan<byte> vertexData);
    void WriteIndexChunk(ReadOnlySpan<byte> indexData);
    void Commit();
    void Rollback();
}
