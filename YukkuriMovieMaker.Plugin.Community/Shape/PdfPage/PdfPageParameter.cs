using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.PdfPage;

internal sealed class PdfPageParameter(SharedDataStore? sharedData) : ShapeParameterBase(sharedData), IFileItem, IResourceItem
{
    private string _file = string.Empty;

    [Display(Name = nameof(Texts.File), ResourceType = typeof(Texts))]
    [PdfFileSelector]
    public string File
    {
        get => _file;
        set => Set(ref _file, value);
    }

    [Display(Name = nameof(Texts.PageNumber), Description = nameof(Texts.PageNumberDescription), ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "", 1, 10)]
    public Animation PageNumber { get; } = new(1, 1, YMM4Constants.VeryLargeValue);

    [Display(Name = nameof(Texts.Scale), Description = nameof(Texts.ScaleDescription), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 1, 200)]
    public Animation Scale { get; } = new(100, 1, YMM4Constants.VeryLargeValue);

    [Display(Name = nameof(Texts.RenderDpi), Description = nameof(Texts.RenderDpiDescription), ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "dpi", 72, 600)]
    public Animation RenderDpi { get; } = new(150, 72, 600);

    public PdfPageParameter() : this(null) { }

    public override IEnumerable<string> CreateMaskExoFilter(
        int keyFrameIndex,
        ExoOutputDescription desc,
        ShapeMaskExoOutputDescription shapeMaskDesc)
    {
        var fps = desc.VideoInfo.FPS;
        return
        [
            $"_name=マスク\r\n" +
            $"_disable={(shapeMaskDesc.IsEnabled ? 0 : 1)}\r\n" +
            $"X={shapeMaskDesc.X.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
            $"Y={shapeMaskDesc.Y.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
            $"回転={shapeMaskDesc.Rotation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
            $"サイズ=100\r\n" +
            $"縦横比=0\r\n" +
            $"ぼかし={shapeMaskDesc.Blur.ToExoString(keyFrameIndex, "F0", fps)}\r\n" +
            $"マスクの反転={(shapeMaskDesc.IsInverted ? 1 : 0):F0}\r\n" +
            $"元のサイズに合わせる=0\r\n" +
            $"type=0\r\n" +
            $"name=\r\n" +
            $"mode=0\r\n"
        ];
    }

    public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc)
        => [];

    public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        => new PdfPageSource(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => [PageNumber, Scale, RenderDpi];

    public IEnumerable<string> GetFiles()
    {
        if (!string.IsNullOrEmpty(File))
            yield return File;
    }

    public void ReplaceFile(string from, string to)
    {
        if (File == from)
            File = to;
    }

    public IEnumerable<TimelineResource> GetResources()
    {
        if (TimelineResource.TryParseFromPath(File, TimelineResourceType.Image, out var resource))
            yield return resource;
    }

    protected override void LoadSharedData(SharedDataStore store)
    {
        var sharedData = store.Load<SharedData>();
        sharedData?.CopyTo(this);
    }

    protected override void SaveSharedData(SharedDataStore store)
        => store.Save(new SharedData(this));

    private sealed class SharedData
    {
        public string File { get; set; } = string.Empty;
        public Animation PageNumber { get; } = new(1, 1, YMM4Constants.VeryLargeValue);
        public Animation Scale { get; } = new(100, 1, YMM4Constants.VeryLargeValue);
        public Animation RenderDpi { get; } = new(150, 72, 600);

        public SharedData() { }

        public SharedData(PdfPageParameter param)
        {
            File = param.File;
            PageNumber.CopyFrom(param.PageNumber);
            Scale.CopyFrom(param.Scale);
            RenderDpi.CopyFrom(param.RenderDpi);
        }

        public void CopyTo(PdfPageParameter param)
        {
            param.File = File;
            param.PageNumber.CopyFrom(PageNumber);
            param.Scale.CopyFrom(Scale);
            param.RenderDpi.CopyFrom(RenderDpi);
        }
    }
}
