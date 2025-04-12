﻿using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleText
{
    internal class ShuffleTextEffectProcessor : IVideoEffectProcessor
    {
        readonly DisposeCollector disposer = new();
        ID2D1CommandList? commandList;
        ID2D1Image? input;
        readonly AffineTransform2D wrap;
        readonly private IGraphicsDevicesAndContext devices;
        readonly ShuffleTextEffect item;

        public ID2D1Image Output { get; }

        public ShuffleTextEffectProcessor(IGraphicsDevicesAndContext devices, ShuffleTextEffect item)
        {
            this.devices = devices;
            this.item = item;

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

            using IDWriteFactory7 factory = DWrite.DWriteCreateFactory<IDWriteFactory7>();
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
            var offset = item.Delay ? 73856093 ^ textIndex * 19349663 : 1;

            var seed = 0;
            if (interval < 1)
            {
                seed = offset * frame + (item.Delay ? textIndex : 0);
            }
            else
            {
                if ((int)(frame % interval) == 0)
                {
                    seed = offset * frame + (item.Delay ? textIndex : 0);
                }
                else
                {
                    var q = (int)(frame / interval);
                    var closestFrame = q * interval;
                    seed = (int)closestFrame * offset + (item.Delay ? textIndex : 0);
                }
            }

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
