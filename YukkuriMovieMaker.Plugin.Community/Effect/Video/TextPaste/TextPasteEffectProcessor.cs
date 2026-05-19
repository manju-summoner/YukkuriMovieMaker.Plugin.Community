using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.TextPaste.TextPaste_Enum;
using YukkuriMovieMaker.Project.Items;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.TextPaste
{
    internal class TextPasteEffectProcessor : IVideoEffectProcessor
    {
        const float PerspectiveDistance = 1000f;
        const float MinimumScale = 1e-5f;

        readonly DisposeCollector disposer = new();
        readonly IGraphicsDevicesAndContext devices;
        readonly TextPasteEffect item;

        readonly AffineTransform2D textTransform;
        readonly ColorMatrix textOpacity;
        readonly ID2D1Image textTransformOutput;
        readonly Composite composite;
        readonly TextItem renderingTextItem;
        readonly ISource renderingTextSource;
        readonly ID2D1Image emptyImage;

        ID2D1CommandList? textCommandList;
        ID2D1Image? input;

        public ID2D1Image Output { get; }

        public TextPasteEffectProcessor(IGraphicsDevicesAndContext devices, TextPasteEffect item)
        {
            this.devices = devices;
            this.item = item;

            textOpacity = new ColorMatrix(devices.DeviceContext);
            disposer.Collect(textOpacity);

            textTransform = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(textTransform);

            textTransformOutput = textTransform.Output;
            disposer.Collect(textTransformOutput);

            composite = new Composite(devices.DeviceContext);
            disposer.Collect(composite);

            Output = composite.Output;
            disposer.Collect(Output);

            renderingTextItem = new TextItem();
            renderingTextSource = renderingTextItem.CreateVideoSource(this.devices, null!);
            disposer.Collect(renderingTextSource);

            var emptyFlood = new Flood(devices.DeviceContext);
            emptyFlood.Color = new Vortice.Mathematics.Color4(0f, 0f, 0f, 0f);
            disposer.Collect(emptyFlood);

            var emptyCrop = new Crop(devices.DeviceContext);
            emptyCrop.SetInput(0, emptyFlood.Output, true);
            emptyCrop.Rectangle = new Vector4(0f, 0f, 1f, 1f);
            disposer.Collect(emptyCrop);

            emptyImage = emptyCrop.Output;
            disposer.Collect(emptyImage);
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            BuildTextCommandList(effectDescription, frame, length, fps);
            UpdateTransform(frame, length, fps);
            ConfigureComposite();

            return effectDescription.DrawDescription;
        }

        void UpdateTransform(int frame, int length, int fps)
        {
            if (textCommandList is null)
                return;

            var opacity = (float)(item.Opacity.GetValue(frame, length, fps) / 100.0);
            textOpacity.Matrix = new Vortice.Mathematics.Matrix5x4
            {
                M11 = 1f,
                M12 = 0f,
                M13 = 0f,
                M14 = 0f,
                M21 = 0f,
                M22 = 1f,
                M23 = 0f,
                M24 = 0f,
                M31 = 0f,
                M32 = 0f,
                M33 = 1f,
                M34 = 0f,
                M41 = 0f,
                M42 = 0f,
                M43 = 0f,
                M44 = opacity,
                M51 = 0f,
                M52 = 0f,
                M53 = 0f,
                M54 = 0f
            };

            var x = (float)item.X.GetValue(frame, length, fps);
            var y = (float)item.Y.GetValue(frame, length, fps);
            var z = (float)item.Z.GetValue(frame, length, fps);
            var zoom = (float)(item.Zoom.GetValue(frame, length, fps) / 100.0);
            var rotationRad = (float)(item.Rotation.GetValue(frame, length, fps) * Math.PI / 180.0);

            var denominator = PerspectiveDistance - z;
            var perspectiveScale = MathF.Abs(denominator) > MinimumScale
                ? PerspectiveDistance / denominator
                : MathF.Sign(denominator) * float.MaxValue;

            var finalZoom = MathF.Max(zoom * perspectiveScale, MinimumScale);

            textTransform.TransformMatrix =
                Matrix3x2.CreateScale(finalZoom) *
                Matrix3x2.CreateRotation(rotationRad) *
                Matrix3x2.CreateTranslation(x, y);
        }

        void ConfigureComposite()
        {
            var validInput = input ?? emptyImage;
            var validTextInput = textCommandList is null ? emptyImage : textTransformOutput;

            switch (item.DisplayMode)
            {
                case TextDisplayMode.Replace:
                    composite.Mode = CompositeMode.SourceOver;
                    composite.SetInput(0, emptyImage, true);
                    composite.SetInput(1, validTextInput, true);
                    break;
                case TextDisplayMode.InsideArea:
                    composite.Mode = CompositeMode.SourceIn;
                    composite.SetInput(0, validInput, true);
                    composite.SetInput(1, validTextInput, true);
                    break;
                case TextDisplayMode.AboveArea:
                    composite.Mode = CompositeMode.SourceAtop;
                    composite.SetInput(0, validInput, true);
                    composite.SetInput(1, validTextInput, true);
                    break;
                default:
                    composite.Mode = CompositeMode.SourceOver;
                    composite.SetInput(0, validInput, true);
                    composite.SetInput(1, validTextInput, true);
                    break;
            }
        }

        void BuildTextCommandList(EffectDescription effectDescription, int frame, int length, int fps)
        {
            renderingTextItem.Text = item.Text;
            renderingTextItem.Decorations = item.Decorations;
            renderingTextItem.Font = item.Font;
            renderingTextItem.FontColor = item.Color;
            renderingTextItem.BasePoint = item.BasePoint;

            if (renderingTextItem.FontSize.Values.Count > 0)
                renderingTextItem.FontSize.Values[0].Value = item.FontSize.GetValue(frame, length, fps);

            if (renderingTextItem.LetterSpacing2.Values.Count > 0)
                renderingTextItem.LetterSpacing2.Values[0].Value = item.CharSpacing.GetValue(frame, length, fps);

            if (renderingTextItem.LineHeight2.Values.Count > 0)
                renderingTextItem.LineHeight2.Values[0].Value = item.LineHeight.GetValue(frame, length, fps);

            var timelineDesc = new TimelineItemSourceDescription(
                new TimelineSourceDescription(
                    default,
                    effectDescription.ItemPosition,
                    effectDescription.ItemDuration,
                    effectDescription.FPS,
                    default,
                    System.Guid.Empty,
                    null!),
                frame,
                length,
                0);

            renderingTextSource.Update(timelineDesc);

            var dc = devices.DeviceContext;

            if (textCommandList is not null)
            {
                textOpacity.SetInput(0, null, true);
                disposer.RemoveAndDispose(ref textCommandList);
            }

            textCommandList = dc.CreateCommandList();
            disposer.Collect(textCommandList);

            var oldTarget = dc.Target;
            dc.Target = textCommandList;
            dc.BeginDraw();
            dc.Clear(null);

            foreach (var output in renderingTextSource.Outputs)
            {
                dc.Transform = Matrix3x2.CreateTranslation(output.DrawingOffset.X, output.DrawingOffset.Y);
                if (output.DrawingEffect is not null)
                {
                    output.DrawingEffect.SetInput(output.Output);
                    output.DrawingEffect.Update(effectDescription);
                    dc.DrawImage(output.DrawingEffect.Output);
                }
                else
                {
                    dc.DrawImage(output.Output);
                }
            }

            dc.Transform = Matrix3x2.Identity;
            dc.EndDraw();
            dc.Target = oldTarget;
            textCommandList.Close();

            textOpacity.SetInput(0, textCommandList, true);
            textTransform.SetInput(0, textOpacity.Output, true);
        }

        public void ClearInput()
            => input = null;

        public void SetInput(ID2D1Image? input)
            => this.input = input;

        public void Dispose()
        {
            composite.SetInput(0, null, true);
            composite.SetInput(1, null, true);
            textOpacity.SetInput(0, null, true);
            textTransform.SetInput(0, null, true);
            disposer.Dispose();
        }
    }
}
