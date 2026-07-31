using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes;

// --- コンテナ定義 ---

public class SingleInputs : InputsContainer
{
    private float _a;

    [InputPort("A", "入力 A")]
    [NumberPortControl(Min = -10000, Max = 10000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float A
    {
        get => _a;
        set => Set(ref _a, value);
    }
}

public class AddInputs : InputsContainer
{
    private float _a;
    private float _b;

    [InputPort("A", "入力 A")]
    [NumberPortControl(Min = -10000, Max = 10000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float A
    {
        get => _a;
        set => Set(ref _a, value);
    }

    [InputPort("B", "入力 B")]
    [NumberPortControl(Min = -10000, Max = 10000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float B
    {
        get => _b;
        set => Set(ref _b, value);
    }
}

// --- ノード本体 ---

[Node("テスト", "動的ポートテスト", "Test node for dynamic port feature")]
public class DynamicPortTestNode : NodeLogic
{
    public enum TestMode
    {
        [Display(Name = "単独数値")] Single,
        [Display(Name = "二数加算")] Add
    }

    private readonly AddInputs _addInputs = new();
    private readonly SingleInputs _singleInputs = new();

    private TestMode _mode = TestMode.Single;

    [InputPort("モード", "Single: A のみ / Add: A + B")]
    [EnumPortControl(Items = typeof(TestMode))]
    [PortColorSetting]
    public TestMode Mode
    {
        get => GetInput<TestMode>();
        set => SetInput(value);
    }

    [InputPort("値", "説明")]
    [ToggleSlider]
    [PortColorSetting]
    public bool Value
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [InputPort("パラメータ", "モードに応じた入力群", isDynamic: true)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public InputsContainer Params
    {
        get => _mode == TestMode.Single ? _singleInputs : _addInputs;
        set => SetDynamicContainer(value);
    }

    [OutputPort("結果", "計算結果")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected internal override void OnInputValueChanged(string portName, object? value)
    {
        if (portName != nameof(Mode)) return;
        var mode = value is TestMode m ? m : (TestMode)Convert.ToInt32(value);
        if (mode == _mode) return;
        _mode = mode;
        SetDynamicContainer(_mode == TestMode.Single ? _singleInputs : _addInputs, nameof(Params));
    }

    protected override Task Calculate()
    {
        var a = Inputs.TryGetValue("Params.A", out var portA)
            ? portA.GetValue(EvaluationContext).GetAwaiter().GetResult() is float fa ? fa : 0f
            : 0f;

        var b = _mode == TestMode.Add && Inputs.TryGetValue("Params.B", out var portB)
            ? portB.GetValue(EvaluationContext).GetAwaiter().GetResult() is float fb ? fb : 0f
            : 0f;

        Result = _mode switch
        {
            TestMode.Single => a,
            TestMode.Add => a + b,
            _ => a
        };

        return Task.CompletedTask;
    }
}