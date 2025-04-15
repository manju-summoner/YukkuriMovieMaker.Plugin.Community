using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleTextInOut.ShffleTextInOut_Enum;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleTextInOut
{
    internal class ShuffleTextInOutEffectProcessor : IVideoEffectProcessor
    {
        readonly DisposeCollector disposer = new ();
        ID2D1CommandList? commandList;
        ID2D1Image? input;
        readonly IDWriteFactory7 factory;
        readonly AffineTransform2D wrap;
        readonly private IGraphicsDevicesAndContext devices;
        readonly ShuffleTextInOutEffect item;

        public ID2D1Image Output { get; }

        public ShuffleTextInOutEffectProcessor(IGraphicsDevicesAndContext devices, ShuffleTextInOutEffect item)
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
            var textCount = effectDescription.InputCount;

            bool enter = true, exit = true;

            if (item.Enum_DisplayMode == DisplayMode.Nomal)
            {
                enter = item.EffectEnter && item.T >= (double)frame / fps;
                exit = item.EffectExit && (double)length / fps - item.T <= (double)frame / fps;
            }

            if (item.Enum_DisplayMode == DisplayMode.Order)
            {
                if (item.Back)
                {
                    var t = Math.Max(item.T - item.DisplayStartTime, 0) / textCount;
                    enter = item.EffectEnter && item.DisplayStartTime + t * (textCount - textIndex) >= (double)frame / fps;
                    exit = item.EffectExit && (double)length / fps - item.DisplayStartTime - t * textIndex <= (double)frame / fps;
                }
                else
                {
                    var t = Math.Max(item.T - item.DisplayStartTime, 0) / textCount;
                    enter = item.EffectEnter && item.DisplayStartTime + t * textIndex >= (double)frame / fps;
                    exit = item.EffectExit && (double)length / fps - item.DisplayStartTime - t * (textCount - textIndex) <= (double)frame / fps;
                }

            }

            if (enter || exit)
            {
                var fontSize = item.FontSize.GetValue(frame, length, fps);
                var fontWeight = item.Bold ? FontWeight.Bold : FontWeight.Normal;
                var fontStyle = item.Italic ? FontStyle.Italic : FontStyle.Normal;
                using var textFormat = factory.CreateTextFormat(item.Font, fontWeight, fontStyle, FontStretch.Normal, (float)fontSize);

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
                var seed = new Random(num).Next();

                if (Enum.TryParse<CharType>(item.Enum_Mode.ToString(), out var textType))
                {
                    text = RandomText.Generate(textType, new Random(seed % int.MaxValue), item);
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
            }
            else
            {
                wrap.TransformMatrix = Matrix3x2.Identity;
                wrap.SetInput(0, input, true);
            }

            return effectDescription.DrawDescription;
        }

        public void ClearInput()
        {
        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
        }

        public void Dispose()
        {
            wrap?.SetInput(0, null, true);
            disposer.Dispose();
        }
    }
}
