using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Settings;
using YukkuriMovieMaker.Player.Video;
using MathNet.Numerics.Random;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleText
{
    internal class ShuffleTextEffectProcessor : IVideoEffectProcessor
    {
        readonly DisposeCollector disposer = new();
        ID2D1CommandList? commandList;
        readonly IDWriteFactory7 factory;
        readonly AffineTransform2D wrap;
        readonly private IGraphicsDevicesAndContext devices;
        readonly ShuffleTextEffect item;

        public ID2D1Image Output { get; }

        public ShuffleTextEffectProcessor(IGraphicsDevicesAndContext devices, ShuffleTextEffect item)
        {
            this.devices = devices;
            this.item = item;

            factory = DWrite.DWriteCreateFactory<IDWriteFactory7>();
            disposer.Collect(factory);

            wrap = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(wrap);

            Output = wrap.Output;
            disposer.Collect(Output);
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var textIndex = effectDescription.InputIndex;
            var font =
                FontSettings.Default.SystemFonts.Concat(FontSettings.Default.CustomFonts)
                .Where(f=>f.FontName == item.Font)
                .DefaultIfEmpty(new Font())
                .First();

            var fontSize = (float)item.FontSize.GetValue(frame, length, fps);
            var fontWeight = (Vortice.DirectWrite.FontWeight)((item.Bold && font.CanonicalFontWeight < Settings.FontWeight.Bold) ? Settings.FontWeight.Bold : font.CanonicalFontWeight);
            var fontStyle = (Vortice.DirectWrite.FontStyle)(item.Italic ? Settings.FontStyle.Italic : font.CanonicalFontStyle);

            using var textFormat = factory.CreateTextFormat(font.CanonicalFontName, fontWeight, fontStyle, (Vortice.DirectWrite.FontStretch)font.CanonicalFontStretch, fontSize);

            var color = item.Color;
            var R = color.R / 255f;
            var G = color.G / 255f;
            var B = color.B / 255f;
            var A = color.A / 255f;
            using var brush = devices.DeviceContext.CreateSolidColorBrush(new Color(R, G, B, A), null);

            var text = "";
            var interval = item.Interval.GetValue(frame, length, fps) * fps;

            int section;
            if ((int)interval == 0)
                section = frame;
            else
                section = frame / (int)interval;
            int num = (item.Delay ? ((section + textIndex) * (section + textIndex)) + textIndex : section * section);

            if (Enum.TryParse<CharType>(item.Enum_Mode.ToString(), out var textType))
            {
                text = RandomText.Generate(textType, new MersenneTwister(num), item);
            }
            else
            {
                text = "?";
            }



            if (commandList != null)
                disposer.RemoveAndDispose(ref commandList);

            var dc = devices.DeviceContext;
            commandList = dc.CreateCommandList();
            disposer.Collect(commandList);
            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawText(text, textFormat, new Rect(0, 0, float.MaxValue, float.MaxValue), brush);
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();
            var commandListRange = devices.DeviceContext.GetImageLocalBounds(commandList);
            var x = -(commandListRange.Left + commandListRange.Right) / 2;
            var y = -(commandListRange.Top + commandListRange.Bottom) / 2;
            wrap.TransformMatrix = Matrix3x2.CreateTranslation(x, y);
            wrap.SetInput(0, commandList, true);

            return effectDescription.DrawDescription;
        }

        public void ClearInput()
        {
        }

        public void SetInput(ID2D1Image? input)
        {

        }

        public void Dispose()
        {
            wrap?.SetInput(0, null, true);
            disposer.Dispose();
        }
    }
}
