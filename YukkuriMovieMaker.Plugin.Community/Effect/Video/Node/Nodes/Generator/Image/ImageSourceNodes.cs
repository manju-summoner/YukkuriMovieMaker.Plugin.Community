using System.IO;
using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Image;

[Node(typeof(ImageSourceCategory), nameof(TextNode.LoadImageNode), nameof(TextNode.LoadImageNodeDescription),
    typeof(TextNode))]
public class LoadImageNode : NodeLogic
{
    private ID2D1Image? _image;
    private string? _loadedPath;
    private DateTime _loadedWriteTimeUtc;

    [InputPort(nameof(TextNode.FilePathPortLabel), nameof(TextNode.LoadImageFilePathDescription), typeof(TextNode))]
    [FileSelector(FileGroupType.ImageItem)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string FilePath
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.LoadImageOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
            return Task.FromException(new NullReferenceException(nameof(FilePath)));

        var writeTimeUtc = File.GetLastWriteTimeUtc(FilePath);
        if (_image is null || _loadedPath != FilePath || _loadedWriteTimeUtc != writeTimeUtc)
        {
            _image?.Dispose();
            _image = ImageLoader.LoadImage(EvaluationContext.Devices, FilePath);
            _loadedPath = FilePath;
            _loadedWriteTimeUtc = writeTimeUtc;
        }

        Output = new ImageWrapper { Image = _image };
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _image?.Dispose();
        _image = null;
        _loadedPath = null;
        base.Dispose();
    }
}

[Node(typeof(ImageSourceCategory), nameof(TextNode.LoadVideoFrameNode), nameof(TextNode.LoadVideoFrameNodeDescription),
    typeof(TextNode))]
public class LoadVideoFrameNode : NodeLogic
{
    private string? _loadedPath;
    private ImageLoader.VideoLoader? _loader;

    [InputPort(nameof(TextNode.FilePathPortLabel), nameof(TextNode.LoadVideoFrameFilePathDescription),
        typeof(TextNode))]
    [FileSelector(FileGroupType.VideoItem)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string FilePath
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FrameIndexLabel), nameof(TextNode.FrameIndexDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 1_000_000, Digits = 0, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float FrameIndex
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.LoadVideoFrameOutputDescription),
        typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
            return Task.FromException(new NullReferenceException(nameof(FilePath)));

        if (_loader is null || _loadedPath != FilePath)
        {
            _loader?.Dispose();
            _loader = ImageLoader.CreateVideoLoader(EvaluationContext.Devices, FilePath);
            _loadedPath = FilePath;
        }

        if (_loader is null)
            return Task.FromException(new NullReferenceException(nameof(FilePath)));

        var frame = Math.Clamp((int)FrameIndex, 0, Math.Max(0, _loader.Length - 1));
        Output = new ImageWrapper { Image = _loader.LoadImage(frame) };
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _loader?.Dispose();
        _loader = null;
        _loadedPath = null;
        base.Dispose();
    }
}