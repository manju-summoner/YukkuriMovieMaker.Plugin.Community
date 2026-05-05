using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using YukkuriMovieMaker.Project.Effects;
using YukkuriMovieMaker.Resources.Localization;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Brush;

[Node(typeof(BrushCategory), "SolidColorBrushPluginName", "SolidColorBrushPluginName",
    typeof(Texts))]
public sealed class SolidColorBrushNode : NodeLogic
{
    private ID2D1SolidColorBrush? _brush;
    private IGraphicsDevicesAndContext? _lastDevices;

    [InputPort("SolidColorBrushParameterColorName", "", typeof(Texts))]
    [ColorPortControl]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public Color InputColor
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextUi.Output), "", typeof(TextUi))]
    [PortColorSetting(nameof(Colors.LawnGreen))]
    public BrushWrapper? Output
    {
        get => GetOutput<BrushWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));

        var color = GetInput<Color>(nameof(InputColor));
        var devices = EvaluationContext.Devices;

        // デバイスが変わったときはブラシを作り直す
        if (_brush == null || !ReferenceEquals(_lastDevices, devices))
        {
            _brush?.Dispose();
            _brush = devices.DeviceContext.CreateSolidColorBrush(ToColor4(color));
            _lastDevices = devices;
        }
        else
        {
            _brush.Color = ToColor4(color);
        }

        Output = new BrushWrapper { Brush = _brush };
        return Task.CompletedTask;
    }

    private static Color4 ToColor4(Color c)
    {
        return new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    }
}

[Node(typeof(BrushCategory), "NoiseBrushPluginName", "NoiseBrushPluginName",
    typeof(Texts))]
public sealed class NoiseNode : NodeLogic
{
    private VideoEffectsLoader? _brush;
    private IGraphicsDevicesAndContext? _lastDevices;

    // --- ポート定義 -------------------------------------------------------

    [InputPort("NoiseBrushParameterNoiseTypeName", "", typeof(Texts))]
    [EnumPortControl(Items = typeof(NoiseType), IsEditable = false, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int NoiseTypePort
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort("NoiseBrushParameterColor1Name", "", typeof(Texts))]
    [ColorPortControl]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public Color Color1
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [InputPort("NoiseBrushParameterColor2Name", "", typeof(Texts))]
    [ColorPortControl]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public Color Color2
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseStrengthName", "", typeof(Texts))]
    [NumberPortControl(Min = 0f, Max = 200f, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Strength
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseThresholdName", "", typeof(Texts))]
    [NumberPortControl(Min = 0f, Max = 100f, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Threshold
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseLevelsName", "", typeof(Texts))]
    [NumberPortControl(Min = 0f, Max = 256f, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Levels
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseOctavesName", "", typeof(Texts))]
    [NumberPortControl(Min = 0f, Max = 32f, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Octaves
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseXName", "", typeof(Texts))]
    [NumberPortControl(Min = -100000f, Max = 100000f, Digits = 1, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float X
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseYName", "", typeof(Texts))]
    [NumberPortControl(Min = -100000f, Max = 100000f, Digits = 1, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseZName", "", typeof(Texts))]
    [NumberPortControl(Min = -100000f, Max = 100000f, Digits = 1, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Z
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseSpeedXName", "", typeof(Texts))]
    [NumberPortControl(Min = -100000f, Max = 100000f, Digits = 2, Unit = "px/f")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float SpeedX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseSpeedYName", "", typeof(Texts))]
    [NumberPortControl(Min = -100000f, Max = 100000f, Digits = 2, Unit = "px/f")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float SpeedY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseSpeedZName", "", typeof(Texts))]
    [NumberPortControl(Min = -100000f, Max = 100000f, Digits = 2, Unit = "px/f")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float SpeedZ
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseScaleXName", "", typeof(Texts))]
    [NumberPortControl(Min = 0f, Max = 100000f, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseScaleYName", "", typeof(Texts))]
    [NumberPortControl(Min = 0f, Max = 100000f, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseScaleZName", "", typeof(Texts))]
    [NumberPortControl(Min = 0f, Max = 100000f, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleZ
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("BasicNoiseParameterBaseAngleName", "", typeof(Texts))]
    [NumberPortControl(Min = -100000f, Max = 100000f, Digits = 1, Unit = "°")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Angle
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("NoiseBrushParameterIsColorName", "", typeof(Texts))]
    [BoolPortControl]
    public bool IsColor
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [InputPort("NoiseBrushParameterIsUniqueSeedName", "", typeof(Texts))]
    [BoolPortControl]
    public bool IsUniqueSeed
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextUi.Output), "", typeof(TextUi))]
    [PortColorSetting(nameof(Colors.LawnGreen))]
    public BrushWrapper? Output
    {
        get => GetOutput<BrushWrapper>();
        set => SetOutput(value);
    }

    // --- Calculate --------------------------------------------------------

    protected override async Task Calculate()
    {
        if (EvaluationContext is null)
        {
            await Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
            return;
        }

        if (_brush == null || !ReferenceEquals(_lastDevices, EvaluationContext.Devices))
        {
            _brush?.Dispose();
            _brush = VideoEffectsLoader.LoadBrushSync("NoiseBrushPlugin", EvaluationContext);
            _lastDevices = EvaluationContext.Devices;
        }

        await _brush.SetValue("NoiseType", (NoiseType)GetInput<int>(nameof(NoiseTypePort)));
        await _brush.SetValue("Color1", GetInput<Color>(nameof(Color1)));
        await _brush.SetValue("Color2", GetInput<Color>(nameof(Color2)));
        await _brush.SetValue("Strength", GetInput<float>(nameof(Strength)));
        await _brush.SetValue("Threshold", GetInput<float>(nameof(Threshold)));
        await _brush.SetValue("Levels", GetInput<float>(nameof(Levels)));
        await _brush.SetValue("Octaves", GetInput<float>(nameof(Octaves)));
        await _brush.SetValue("X", GetInput<float>(nameof(X)));
        await _brush.SetValue("Y", GetInput<float>(nameof(Y)));
        await _brush.SetValue("Z", GetInput<float>(nameof(Z)));
        await _brush.SetValue("SpeedX", GetInput<float>(nameof(SpeedX)));
        await _brush.SetValue("SpeedY", GetInput<float>(nameof(SpeedY)));
        await _brush.SetValue("SpeedZ", GetInput<float>(nameof(SpeedZ)));
        await _brush.SetValue("ScaleX", GetInput<float>(nameof(ScaleX)));
        await _brush.SetValue("ScaleY", GetInput<float>(nameof(ScaleY)));
        await _brush.SetValue("ScaleZ", GetInput<float>(nameof(ScaleZ)));
        await _brush.SetValue("Angle", GetInput<float>(nameof(Angle)));
        await _brush.SetValue("IsColor", GetInput<bool>(nameof(IsColor)));
        await _brush.SetValue("IsUniqueSeed", GetInput<bool>(nameof(IsUniqueSeed)));

        if (_brush.Update(out var brush, EvaluationContext.EffectDescription))
            Output = new BrushWrapper { Brush = brush };
    }
}