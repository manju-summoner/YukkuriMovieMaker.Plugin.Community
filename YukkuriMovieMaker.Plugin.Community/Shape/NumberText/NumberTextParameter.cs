using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.NumberText;

internal class NumberTextParameter(SharedDataStore? sharedData) : ShapeParameterBase(sharedData)
{
    private Color _color = Colors.White;
    private string _font = "MS UI Gothic";
    private bool _isBold;
    private bool _isItalic;
    private bool _separate;

    private TextAlignment _textAlignment = TextAlignment.LeftTop;

    [Display(Name = nameof(Texts.Value), Description = nameof(Texts.ValueToDisplay), ResourceType = typeof(Texts))]
    [AnimationSlider("F4", "", -100, 100)]
    public Animation Number { get; } = new(0);

    [Display(Name = nameof(Texts.IntegerDigits), ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "", 1, 4)]
    public Animation IntegerDigits { get; } = new(1, 1, 16);

    [Display(Name = nameof(Texts.DecimalDigits), ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "", 0, 4)]
    public Animation DecimalDigits { get; } = new (0, 0, 16);

    [Display(Name = nameof(Texts.ThousandsSeparator), ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool Separate
    {
        get => _separate;
        set => Set(ref _separate, value);
    }

    [Display(GroupName = nameof(Texts.Text), Name = nameof(Texts.Font), ResourceType = typeof(Texts))]
    [FontComboBox]
    public string Font
    {
        get => _font;
        set => Set(ref _font, value);
    }

    [Display(GroupName = nameof(Texts.Text), Name = nameof(Texts.Size), Description = nameof(Texts.SizeOfCharacters), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", 1, 100)]
    public Animation FontSize { get; } = new(32, 1, YMM4Constants.VeryLargeValue);

    [Display(GroupName = nameof(Texts.Text), Name = nameof(Texts.Justification), ResourceType = typeof(Texts))]
    [EnumComboBox]
    public TextAlignment Alignment
    {
        get => _textAlignment;
        set => Set(ref _textAlignment, value);
    }

    [Display(GroupName = nameof(Texts.Text), Name = nameof(Texts.ColorOfCharacters), ResourceType = typeof(Texts))]
    [ColorPicker]
    public Color Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    [Display(GroupName = nameof(Texts.Text), Name = nameof(Texts.Bold), ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool IsBold
    {
        get => _isBold;
        set => Set(ref _isBold, value);
    }

    [Display(GroupName = nameof(Texts.Text), Name = nameof(Texts.Italic), ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool IsItalic
    {
        get => _isItalic;
        set => Set(ref _isItalic, value);
    }

    /// <summary>
    /// コンストラクタ（JsonSerizlier用）
    /// </summary>
    public NumberTextParameter() : this(null)
    {

    }

    public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc,
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
    {
        return [];
    }

    public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
    {
        return new NumberTextSource(devices, this);
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
    {
        return [Number, IntegerDigits, DecimalDigits, FontSize];
    }

    protected override void LoadSharedData(SharedDataStore store)
    {
        var sharedData = store.Load<SharedData>();
        sharedData?.CopyTo(this);
    }

    protected override void SaveSharedData(SharedDataStore store)
    {
        store.Save(new SharedData(this));
    }

    private class SharedData
    {
        public SharedData(NumberTextParameter param)
        {
            Number.CopyFrom(param.Number);
            IntegerDigits.CopyFrom(param.IntegerDigits);
            DecimalPlaces.CopyFrom(param.DecimalDigits);
            FontSize.CopyFrom(param.FontSize);
        }

        private Animation Number { get; } = new(0);
        private Animation IntegerDigits { get; } = new(0, 0, 16);
        private Animation DecimalPlaces { get; } = new(0, 0, 16);
        private Animation FontSize { get; } = new(32, 1, YMM4Constants.VeryLargeValue);

        public void CopyTo(NumberTextParameter param)
        {
            param.Number.CopyFrom(Number);
            param.IntegerDigits.CopyFrom(IntegerDigits);
            param.DecimalDigits.CopyFrom(DecimalPlaces);
            param.FontSize.CopyFrom(FontSize);
        }
    }
}