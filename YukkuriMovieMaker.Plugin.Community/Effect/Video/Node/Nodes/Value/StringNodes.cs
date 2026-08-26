using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Value;

public enum StringEncodingKind
{
    [Display(Name = nameof(TextNode.EncodingUtf8), ResourceType = typeof(TextNode))]
    Utf8,

    [Display(Name = nameof(TextNode.EncodingUtf16), ResourceType = typeof(TextNode))]
    Utf16,

    [Display(Name = nameof(TextNode.EncodingUtf32), ResourceType = typeof(TextNode))]
    Utf32,

    [Display(Name = nameof(TextNode.EncodingShiftJis), ResourceType = typeof(TextNode))]
    ShiftJis,

    [Display(Name = nameof(TextNode.EncodingAscii), ResourceType = typeof(TextNode))]
    Ascii
}

public enum NumberIntegerPaddingKind
{
    [Display(Name = nameof(TextNode.IntegerPaddingZero), ResourceType = typeof(TextNode))]
    Zero,

    [Display(Name = nameof(TextNode.IntegerPaddingSpace), ResourceType = typeof(TextNode))]
    Space
}

public enum NumberZeroFormatKind
{
    [Display(Name = nameof(TextNode.ZeroFormatInclude), ResourceType = typeof(TextNode))]
    Include,

    [Display(Name = nameof(TextNode.ZeroFormatOmit), ResourceType = typeof(TextNode))]
    Omit
}

internal static class StringEncodingUtility
{
    static StringEncodingUtility()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding Resolve(StringEncodingKind kind)
    {
        return kind switch
        {
            StringEncodingKind.Utf8 => new UTF8Encoding(false),
            StringEncodingKind.Utf16 => Encoding.Unicode,
            StringEncodingKind.Utf32 => Encoding.UTF32,
            StringEncodingKind.ShiftJis => Encoding.GetEncoding("shift_jis"),
            StringEncodingKind.Ascii => Encoding.ASCII,
            _ => Encoding.UTF8
        };
    }
}

[Node(typeof(StringCategory), nameof(TextNode.UnicodeToCharNode), nameof(TextNode.UnicodeToCharNodeDescription),
    typeof(TextNode))]
public class UnicodeToCharNode : NodeLogic
{
    [InputPort(nameof(TextNode.CodePoint), nameof(TextNode.CodePointDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 0x10FFFF, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int CodePoint
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var isSurrogate = CodePoint is >= 0xD800 and <= 0xDFFF;
        Result = CodePoint is >= 0 and <= 0x10FFFF && !isSurrogate
            ? char.ConvertFromUtf32(CodePoint)
            : "";
        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.CharToUnicodeNode), nameof(TextNode.CharToUnicodeNodeDescription),
    typeof(TextNode))]
public class CharToUnicodeNode : NodeLogic
{
    [InputPort(nameof(TextNode.Character), nameof(TextNode.CharacterDescription), typeof(TextNode))]
    [TextPortControl(Default = "A")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Character
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Result
    {
        get => GetOutput<int>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        try
        {
            Result = string.IsNullOrEmpty(Character) ? 0 : char.ConvertToUtf32(Character, 0);
        }
        catch (ArgumentException)
        {
            Result = 0;
        }

        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringNthCharNode), nameof(TextNode.StringNthCharNodeDescription),
    typeof(TextNode))]
public class StringNthCharNode : NodeLogic
{
    [InputPort(nameof(TextNode.Text), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Index), nameof(TextNode.IndexDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = int.MaxValue, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Index
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var e = StringInfo.GetTextElementEnumerator(Text);
        var i = 0;
        Result = "";
        while (e.MoveNext())
        {
            if (i == Index)
            {
                Result = (string)e.Current;
                break;
            }

            i++;
        }

        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringEncodeNode), nameof(TextNode.StringEncodeNodeDescription),
    typeof(TextNode))]
public class StringEncodeNode : NodeLogic
{
    [InputPort(nameof(TextNode.Text), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Encoding), nameof(TextNode.EncodingDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(StringEncodingKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public StringEncodingKind Encoding
    {
        get => GetInput<StringEncodingKind>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Bytes), nameof(TextNode.BytesDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public byte[] Result
    {
        get => GetOutput<byte[]>() ?? [];
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = StringEncodingUtility.Resolve(Encoding).GetBytes(Text);
        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringDecodeNode), nameof(TextNode.StringDecodeNodeDescription),
    typeof(TextNode))]
public class StringDecodeNode : NodeLogic
{
    [InputPort(nameof(TextNode.Bytes), nameof(TextNode.BytesDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public byte[] Bytes
    {
        get => GetInput<byte[]>() ?? [];
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Encoding), nameof(TextNode.EncodingDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(StringEncodingKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public StringEncodingKind Encoding
    {
        get => GetInput<StringEncodingKind>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        try
        {
            Result = StringEncodingUtility.Resolve(Encoding).GetString(Bytes);
        }
        catch (DecoderFallbackException)
        {
            Result = "";
        }

        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.NumberToStringNode), nameof(TextNode.NumberToStringNodeDescription),
    typeof(TextNode))]
public class NumberToStringNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -1000000, Max = 1000000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MinIntegerDigits), nameof(TextNode.MinIntegerDigitsDescription), typeof(TextNode))]
    [NumberPortControl(Min = 1, Max = 20, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int MinIntegerDigits
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FractionDigits), nameof(TextNode.FractionDigitsDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 20, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int FractionDigits
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.IntegerPadding), nameof(TextNode.IntegerPaddingDescription), typeof(TextNode))]
    [EnumPortControl(Default = 1, IsEditable = false, Items = typeof(NumberIntegerPaddingKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public NumberIntegerPaddingKind IntegerPadding
    {
        get => GetInput<NumberIntegerPaddingKind>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ZeroFormat), nameof(TextNode.ZeroFormatDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(NumberZeroFormatKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public NumberZeroFormatKind ZeroFormat
    {
        get => GetInput<NumberZeroFormatKind>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var minIntegerDigits = Math.Max(1, MinIntegerDigits);
        var fractionDigits = Math.Max(0, FractionDigits);

        var integerFormat = IntegerPadding switch
        {
            NumberIntegerPaddingKind.Zero =>
                new string('0', minIntegerDigits),

            NumberIntegerPaddingKind.Space =>
                new string('#', minIntegerDigits),

            _ =>
                new string('0', minIntegerDigits)
        };

        var fractionFormat = fractionDigits > 0
            ? "." + new string('0', fractionDigits)
            : "";

        var format = integerFormat + fractionFormat;

        var result = Value.ToString(
            format,
            CultureInfo.InvariantCulture);

        if (IntegerPadding == NumberIntegerPaddingKind.Space)
        {
            var signLength = result.StartsWith('-')
                ? 1
                : 0;
            var integerEnd = result.IndexOf('.');
            if (integerEnd < 0)
                integerEnd = result.Length;

            var integerLength = integerEnd - signLength;
            if (integerLength < minIntegerDigits)
            {
                var paddingLength =
                    minIntegerDigits - integerLength;

                result = result.Insert(
                    signLength,
                    new string(' ', paddingLength));
            }
        }

        if (ZeroFormat == NumberZeroFormatKind.Omit)
        {
            if (result.StartsWith("0.", StringComparison.Ordinal))
                result = result[1..];
            else if (result.StartsWith("-0.", StringComparison.Ordinal)) result = "-" + result[2..];
        }

        Result = result;
        return Task.CompletedTask;
    }
}

[Node(typeof(StringCategory), nameof(TextNode.StringConcatNode), nameof(TextNode.StringConcatNodeDescription),
    typeof(TextNode))]
public class StringConcatNode : NodeLogic
{
    [InputPort(nameof(TextNode.StringConcatInput1), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text1
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StringConcatInput2), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text2
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StringConcatInput3), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text3
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StringConcatInput4), nameof(TextNode.TextDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text4
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StringConcatSeparator), nameof(TextNode.StringConcatSeparatorDescription),
        typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Separator
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = string.Join(Separator, Text1, Text2, Text3, Text4);
        return Task.CompletedTask;
    }
}