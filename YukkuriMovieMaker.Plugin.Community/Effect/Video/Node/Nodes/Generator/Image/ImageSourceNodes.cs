using System.IO;
using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Image;

[Node(typeof(ImageSourceCategory), "画像読み込み", "指定したファイルから画像を読み込みます。")]
public class LoadImageNode : NodeLogic
{
    private ID2D1Image? _image;
    private string? _loadedPath;
    private DateTime _loadedWriteTimeUtc;

    [InputPort("ファイルパス", "読み込む画像ファイル")]
    [FilePathPortControl(AllowExtension = ["画像ファイル|.png;.jpg;.jpeg;.bmp;.gif;.tiff;.tif;.webp"])]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string FilePath
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort("出力画像", "読み込んだ画像")]
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

[Node(typeof(ImageSourceCategory), "動画フレーム読み込み", "指定した動画ファイルから任意のフレームを画像として読み込みます。")]
public class LoadVideoFrameNode : NodeLogic
{
    private string? _loadedPath;
    private ImageLoader.VideoLoader? _loader;

    [InputPort("ファイルパス", "読み込む動画ファイル")]
    [FilePathPortControl(AllowExtension = ["動画ファイル|.mp4;.mov;.avi;.wmv;.mkv;.webm;.m4v"])]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string FilePath
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort("フレーム番号", "読み込むフレームの番号（0始まり）")]
    [NumberPortControl(Min = 0, Max = 1_000_000, Digits = 0, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float FrameIndex
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("出力画像", "読み込んだフレーム画像")]
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

        var frame = System.Math.Clamp((int)FrameIndex, 0, System.Math.Max(0, _loader.Length - 1));
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