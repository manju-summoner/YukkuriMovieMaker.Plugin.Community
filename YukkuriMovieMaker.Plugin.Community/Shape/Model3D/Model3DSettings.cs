using Vortice.DXGI;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Views;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal sealed class Model3DSettings : SettingsBase<Model3DSettings>
{
    public const int MinFileSizeMB = 10;
    public const int MaxFileSizeMBLimit = 10_240;
    public const int DefaultMaxFileSizeMB = 500;

    public const int MinVertices = 10_000;
    public const int MaxVerticesLimit = 100_000_000;
    public const int DefaultMaxVertices = 10_000_000;

    public const int MinIndices = 30_000;
    public const int MaxIndicesLimit = 300_000_000;
    public const int DefaultMaxIndices = 30_000_000;

    public const int MinParts = 10;
    public const int MaxPartsLimit = 50_000;
    public const int DefaultMaxParts = 10_000;

    public const int MinGpuMemoryMB = 64;
    public const int MaxGpuMemoryMBLimit = 32_768;
    public const int DefaultMaxGpuMemoryPerModelMB = 2_048;

    private const long DedicatedVideoMemoryThresholdBytes = 512L * 1024L * 1024L;
    private const long BytesPerMegabyte = 1024L * 1024L;

    private int _maxFileSizeMB = DefaultMaxFileSizeMB;
    private int _maxVertices = DefaultMaxVertices;
    private int _maxIndices = DefaultMaxIndices;
    private int _maxParts = DefaultMaxParts;
    private int _maxGpuMemoryPerModelMB = DefaultMaxGpuMemoryPerModelMB;

    public override string Name => Texts.Model3D;
    public override SettingsCategory Category => SettingsCategory.Shape;
    public override bool HasSettingView => true;
    public override object SettingView => new Model3DSettingsView();

    public int MaxFileSizeMB
    {
        get => _maxFileSizeMB;
        set => Set(ref _maxFileSizeMB, Math.Clamp(value, MinFileSizeMB, MaxFileSizeMBLimit));
    }

    public int MaxVertices
    {
        get => _maxVertices;
        set => Set(ref _maxVertices, Math.Clamp(value, MinVertices, MaxVerticesLimit));
    }

    public int MaxIndices
    {
        get => _maxIndices;
        set => Set(ref _maxIndices, Math.Clamp(value, MinIndices, MaxIndicesLimit));
    }

    public int MaxParts
    {
        get => _maxParts;
        set => Set(ref _maxParts, Math.Clamp(value, MinParts, MaxPartsLimit));
    }

    public int MaxGpuMemoryPerModelMB
    {
        get => _maxGpuMemoryPerModelMB;
        set => Set(ref _maxGpuMemoryPerModelMB, Math.Clamp(value, MinGpuMemoryMB, MaxGpuMemoryMBLimit));
    }

    public long MaxFileSizeBytes => (long)MaxFileSizeMB * BytesPerMegabyte;

    public long MaxGpuMemoryPerModelBytes => (long)MaxGpuMemoryPerModelMB * BytesPerMegabyte;

    public bool IsFileSizeAllowed(long fileBytes) => fileBytes <= MaxFileSizeBytes;

    public bool IsGpuMemoryPerModelAllowed(long gpuBytes) => gpuBytes <= MaxGpuMemoryPerModelBytes;

    public bool IsModelComplexityAllowed(int vertexCount, int indexCount, int partCount)
        => vertexCount <= MaxVertices && indexCount <= MaxIndices && partCount <= MaxParts;

    public override void Initialize() => ClampGpuMemoryToAdapter();

    private void ClampGpuMemoryToAdapter()
    {
        long budgetBytes = GetAdapterMemoryBudgetBytes();
        if (budgetBytes <= 0) return;

        long budgetMB = budgetBytes / BytesPerMegabyte;
        if (budgetMB < MinGpuMemoryMB) return;

        if (MaxGpuMemoryPerModelMB > budgetMB)
            MaxGpuMemoryPerModelMB = (int)Math.Min(budgetMB, MaxGpuMemoryMBLimit);
    }

    private static long GetAdapterMemoryBudgetBytes()
    {
        try
        {
            if (DXGI.CreateDXGIFactory1(out IDXGIFactory1? factory).Failure || factory is null)
                return 0;

            using (factory)
            {
                long dedicatedVideoMemory = 0;
                long sharedSystemMemory = 0;

                for (int i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
                {
                    using (adapter)
                    {
                        var description = adapter.Description1;
                        if ((description.Flags & AdapterFlags.Software) != 0) continue;
                        if ((long)description.DedicatedVideoMemory <= dedicatedVideoMemory) continue;

                        dedicatedVideoMemory = (long)description.DedicatedVideoMemory;
                        sharedSystemMemory = (long)description.SharedSystemMemory;
                    }
                }

                return dedicatedVideoMemory > DedicatedVideoMemoryThresholdBytes
                    ? dedicatedVideoMemory
                    : sharedSystemMemory;
            }
        }
        catch
        {
            return 0;
        }
    }
}
