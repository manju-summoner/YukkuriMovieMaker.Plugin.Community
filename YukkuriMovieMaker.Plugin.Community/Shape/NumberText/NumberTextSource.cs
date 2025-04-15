using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using static Vortice.DirectWrite.DWrite;

#pragma warning disable CS8618, CS9264

namespace YukkuriMovieMaker.Plugin.Community.Shape.NumberText;

internal sealed class NumberTextSource(IGraphicsDevicesAndContext devices, NumberTextParameter numberTextParameter)
    : IShapeSource
{
    private const double Tolerance = 0.001;
    private System.Windows.Media.Color _color;

    private ID2D1CommandList? _commandList;
    private double _integerDigits;
    private double _decimalDigits;

    private bool _disposedValue;
    private string _font;
    private float _fontSize;
    private bool _isBold;
    private bool _isItalic;
    private double _number;
    private bool _separate;
    private TextAlignment _textAlignment;

    public ID2D1Image Output =>
        _commandList ?? throw new Exception($"{nameof(_commandList)}がnullです。事前にUpdateを呼び出す必要があります。");

    public void Update(TimelineItemSourceDescription timelineItemSourceDescription)
    {
        var fps = timelineItemSourceDescription.FPS;
        var frame = timelineItemSourceDescription.ItemPosition.Frame;
        var length = timelineItemSourceDescription.ItemDuration.Frame;

        var number = numberTextParameter.Number.GetValue(frame, length, fps);
        var integerDigits = numberTextParameter.IntegerDigits.GetValue(frame, length, fps);
        var decimalDigits = numberTextParameter.DecimalDigits.GetValue(frame, length, fps);
        var font = numberTextParameter.Font;
        var fontSize = (float)numberTextParameter.FontSize.GetValue(frame, length, fps);
        var textAlignment = numberTextParameter.Alignment;
        var color = numberTextParameter.Color;
        var separate = numberTextParameter.Separate;
        var isBold = numberTextParameter.IsBold;
        var isItalic = numberTextParameter.IsItalic;

        if (fontSize == 0) fontSize = 1;
        if (_commandList != null && Math.Abs(_number - number) < Tolerance &&
            Math.Abs(_integerDigits - integerDigits) < Tolerance &&
            Math.Abs(_decimalDigits - decimalDigits) < Tolerance &&
            Math.Abs(_fontSize - fontSize) < Tolerance && _font == font && _textAlignment == textAlignment &&
            _color == color && _separate == separate && _isBold == isBold && _isItalic == isItalic)
            return;

        var dc = devices.DeviceContext;

        using var formatFactory = DWriteCreateFactory<IDWriteFactory>();
        var textFormat = formatFactory.CreateTextFormat(font, isBold ? FontWeight.Bold : FontWeight.Normal,
            isItalic ? FontStyle.Italic : FontStyle.Normal, fontSize);

        textFormat.WordWrapping = WordWrapping.NoWrap;

        var stringFormat = CreateStringFormat(integerDigits, decimalDigits, separate);
        var text = number.ToString(stringFormat);

        using var layoutFactory = DWriteCreateFactory<IDWriteFactory>();
        var textLayout = layoutFactory.CreateTextLayout(text, textFormat, fontSize * (text.Length + 1), fontSize);

        var width = textLayout.Metrics.Width;
        var height = textLayout.Metrics.Height;
        int x;
        int y;
        switch (textAlignment)
        {
            case TextAlignment.LeftTop:
                x = 0;
                y = 0;
                break;
            case TextAlignment.CenterTop:
                x = -(int)width / 2;
                y = 0;
                break;
            case TextAlignment.RightTop:
                x = -(int)width;
                y = 0;
                break;
            case TextAlignment.LeftCenter:
                x = 0;
                y = -(int)height / 2;
                break;
            case TextAlignment.CenterCenter:
                x = -(int)width / 2;
                y = -(int)height / 2;
                break;
            case TextAlignment.RightCenter:
                x = -(int)width;
                y = -(int)height / 2;
                break;
            case TextAlignment.LeftBottom:
                x = 0;
                y = -(int)height;
                break;
            case TextAlignment.CenterBottom:
                x = -(int)width / 2;
                y = -(int)height;
                break;
            case TextAlignment.RightBottom:
                x = -(int)width;
                y = -(int)height;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(textAlignment), textAlignment, @"不正な値です。");
        }

        var brush = devices.DeviceContext.CreateSolidColorBrush(new Color(color.R / 255.0f, color.G / 255.0f,
            color.B / 255.0f, color.A / 255.0f));

        _commandList?.Dispose();
        _commandList = dc.CreateCommandList();

        dc.Target = _commandList;
        dc.BeginDraw();
        dc.Clear(null);

        dc.DrawTextLayout(new Vector2(x, y), textLayout, brush);

        dc.EndDraw();
        dc.Target = null;
        _commandList.Close();

        _number = number;
        _integerDigits = integerDigits;
        _decimalDigits = decimalDigits;
        _font = font;
        _fontSize = fontSize;
        _textAlignment = textAlignment;
        _color = color;
        _separate = separate;
        _isBold = isBold;
        _isItalic = isItalic;

        brush.Dispose();
    }

    private static string CreateStringFormat(double integerDigits, double decimalDigits, bool separate)
    {
        string text = string.Empty;
        if (separate)
            text = "#,";
        text += new string('0', (int)integerDigits);
        if (decimalDigits > 0)
            text += "." + new string('0', (int)decimalDigits);
        return text;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~NumberTextSource()
    {
        Dispose();
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing) _commandList?.Dispose();

        _disposedValue = true;
    }
}