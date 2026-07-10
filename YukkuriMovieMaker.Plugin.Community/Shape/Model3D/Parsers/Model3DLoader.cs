using System.IO;
using System.Reflection;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal static class Model3DLoader
{
    private const string DefaultPluginVersion = "1.0.0";

    private static readonly IModelParser[] NativeParsers =
    [
        new WavefrontObjParser(),
        new StlParser(),
        new PlyParser(),
        new GlbParser(),
        new ThreeMfParser(),
        new PmxParser(),
        new PmdParser()
    ];

    private static readonly AssimpParser FallbackParser = new();
    private static readonly Dictionary<string, IModelParser> ParsersByExtension = BuildRegistry();
    private static readonly ModelCache Cache = new();
    private static readonly string PluginVersion = ReadPluginVersion();

    public static IReadOnlyCollection<string> SupportedExtensions => ParsersByExtension.Keys;

    public static bool IsSupported(string path)
        => ParsersByExtension.ContainsKey(NormalizeExtension(path));

    public static Model3DData Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new Model3DData();
        if (!ParsersByExtension.TryGetValue(NormalizeExtension(path), out var parser)) return new Model3DData();

        var fileInfo = new FileInfo(path);
        if (!Model3DSettings.Default.IsFileSizeAllowed(fileInfo.Length)) return new Model3DData();

        if (Cache.TryLoad(path, fileInfo.LastWriteTimeUtc, parser.Id, parser.Version, PluginVersion, out var cachedModel)
            && !HasMissingEmbeddedTextures(cachedModel))
        {
            return WithinLimits(cachedModel) ? cachedModel : new Model3DData();
        }

        var model = parser is IStreamingModelParser streamingParser
            ? LoadStreaming(path, streamingParser, fileInfo) ?? ParseAndCache(path, parser, fileInfo)
            : ParseAndCache(path, parser, fileInfo);

        return WithinLimits(model) ? model : new Model3DData();
    }

    private static Model3DData? LoadStreaming(string path, IStreamingModelParser parser, FileInfo fileInfo)
    {
        try
        {
            var header = new CacheHeader(
                fileInfo.LastWriteTimeUtc.ToBinary(),
                path,
                parser.Id,
                parser.Version,
                PluginVersion,
                ModelCache.ComputeFileHash(path));

            using (var writer = Cache.CreateStreamingWriter(path, header))
            {
                parser.StreamToCache(path, writer);
                writer.Commit();
            }

            return Cache.TryLoad(path, fileInfo.LastWriteTimeUtc, parser.Id, parser.Version, PluginVersion, out var model)
                ? model
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static Model3DData ParseAndCache(string path, IModelParser parser, FileInfo fileInfo)
    {
        Model3DData model;
        try
        {
            model = parser.Parse(path);
        }
        catch
        {
            return new Model3DData();
        }

        if (model.Vertices.Length > 0 && WithinLimits(model))
            Cache.Save(path, model, fileInfo.LastWriteTimeUtc, parser.Id, parser.Version, PluginVersion);

        return model;
    }

    private static bool HasMissingEmbeddedTextures(Model3DData model)
    {
        foreach (var part in model.Parts)
        {
            if (ModelHelper.IsEmbeddedTexturePath(part.TexturePath) && !File.Exists(part.TexturePath)) return true;
            if (ModelHelper.IsEmbeddedTexturePath(part.MetallicRoughnessTexturePath) && !File.Exists(part.MetallicRoughnessTexturePath)) return true;
        }
        return false;
    }

    private static bool WithinLimits(Model3DData model)
        => Model3DSettings.Default.IsModelComplexityAllowed(model.Vertices.Length, model.Indices.Length, model.Parts.Count);

    private static Dictionary<string, IModelParser> BuildRegistry()
    {
        var registry = new Dictionary<string, IModelParser>(StringComparer.Ordinal);

        foreach (var parser in NativeParsers)
        {
            foreach (var extension in parser.Extensions)
                registry[extension] = parser;
        }

        foreach (var extension in FallbackParser.Extensions)
            registry.TryAdd(extension, FallbackParser);

        return registry;
    }

    private static string NormalizeExtension(string path) => Path.GetExtension(path).ToLowerInvariant();

    private static string ReadPluginVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? DefaultPluginVersion;
        }
        catch
        {
            return DefaultPluginVersion;
        }
    }
}
