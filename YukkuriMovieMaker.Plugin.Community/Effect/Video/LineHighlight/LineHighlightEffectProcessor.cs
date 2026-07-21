using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LineHighlight
{
    using AlphaMask = Vortice.Direct2D1.Effects.AlphaMask;

    internal class LineHighlightEffectProcessor(IGraphicsDevicesAndContext devices, LineHighlightEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly IGraphicsDevicesAndContext devices = devices;
        readonly LineHighlightEffect item = item;

        ID2D1CommandList? commandList;
        AlphaMask? alphaMask;
        ColorMatrix? colorMatrix;
        Composite? composite;

        bool isFirst = true;
        RawRectF bounds;
        System.Windows.Media.Color color;
        double strength, fade, size, blur, angle, animationRate;
        bool isAnimationActive;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect
                || alphaMask is null
                || colorMatrix is null
                || composite is null
                || input is null)
                return effectDescription.DrawDescription;

            var dc = devices.DeviceContext;

            var bounds = dc.GetImageLocalBounds(input);

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var color = item.Color;
            var strength = item.Strength.GetValue(frame, length, fps) / 100;
            var fade = item.Fade.GetValue(frame, length, fps) / 100;
            var size = item.Size.GetValue(frame, length, fps);
            var blur = item.Blur.GetValue(frame, length, fps);
            var angle = item.Angle.GetValue(frame, length, fps) / 180 * Math.PI;
            var easingType = item.EasingType;
            var easingMode = item.EasingMode;
            var effectDuration = item.EffectDuration.GetValue(frame, length, fps);
            var isLoop = item.IsLoop;
            var loopInterval = item.LoopInterval.GetValue(frame, length, fps);

            double animationPosition;
            if (isLoop == false)
            {
                animationPosition = effectDescription.ItemPosition.Time.TotalSeconds;
            }
            else if(effectDuration + loopInterval != 0)
            {
                animationPosition = effectDescription.ItemPosition.Time.TotalSeconds % (effectDuration + loopInterval);
            }
            else
            {
                animationPosition = 0;
            }
            var isAnimationActive = animationPosition < effectDuration && size > 0;
            var animationRate = effectDuration != 0 ? Easing.GetValue(easingType, easingMode, Math.Clamp(animationPosition / effectDuration, 0, 1)) : 0;

            if (isFirst || ((this.isAnimationActive == true || isAnimationActive == true) && (!this.bounds.Equals(bounds) || this.color != color
                || this.strength != strength || this.fade != fade || this.size != size || this.blur != blur || this.angle != angle || this.animationRate != animationRate)))
            {
                if (commandList is not null)
                {
                    disposer.RemoveAndDispose(ref commandList);
                }
                commandList = dc.CreateCommandList();
                disposer.Collect(commandList);

                if (isAnimationActive)
                {
                    var rotationMatrix = Matrix3x2.CreateRotation((float)angle);
                    var reverseRotationMatrix = Matrix3x2.CreateRotation(-(float)angle);
                    Vector2[] edges = [
                            new Vector2(bounds.Left, bounds.Top),
                            new Vector2(bounds.Right, bounds.Top),
                            new Vector2(bounds.Left, bounds.Bottom),
                            new Vector2(bounds.Right, bounds.Bottom)
                        ];
                    var rotedEdges = edges.Select(x => Vector2.Transform(x, reverseRotationMatrix));
                    float moveBegin = rotedEdges.Min(x => x.Y);
                    float moveEnd = rotedEdges.Max(x => x.Y);
                    var moveMatrix = Matrix3x2.CreateTranslation(
                        new Vector2((float)((moveBegin - (size / 2 + blur / 2)) * (1 - animationRate) + (moveEnd + (size / 2 + blur / 2)) * animationRate)));
                    double fadeRate = animationRate < 0.5 ? animationRate * 2 : 1 - (animationRate - 0.5) * 2;
                    float currentFade = (float)(fadeRate + (1 - fadeRate) * fade);

                    var gradientBrushProperty = new LinearGradientBrushProperties()
                    {
                        StartPoint = new Vector2(0, -(float)(size / 2 + blur / 2)),
                        EndPoint = new Vector2(0, (float)(size / 2 + blur / 2))
                    };
                    var brushProperties = new BrushProperties(currentFade, moveMatrix * rotationMatrix);
                    GradientStop[] stops;
                    if (size >= blur)
                    {
                        stops = [
                            new GradientStop()
                            {
                                Position = 0,
                                Color = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 0)
                            },
                            new GradientStop(){
                                Position = (float)(blur / size * 0.5),
                                Color = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f)
                            },
                            new GradientStop(){
                                Position = (float)(1 - blur / size * 0.5),
                                Color = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f)
                            },
                            new GradientStop()
                            {
                                Position = 1,
                                Color = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 0)
                            }
                        ];
                    }
                    else
                    {
                        stops = [
                            new GradientStop()
                            {
                                Position = 0,
                                Color = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 0)
                            },
                            new GradientStop(){
                                Position = 0.5f,
                                Color = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f * (float)(size / blur))
                            },
                            new GradientStop()
                            {
                                Position = 1,
                                Color = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 0)
                            }
                        ];
                    }
                    using var gradientStopCollection = dc.CreateGradientStopCollection(stops, ExtendMode.Clamp);
                    using var brush = dc.CreateLinearGradientBrush(gradientBrushProperty, brushProperties, gradientStopCollection);

                    dc.Target = commandList;
                    dc.BeginDraw();
                    dc.FillRectangle(bounds, brush);
                    dc.EndDraw();
                    dc.Target = null;
                    commandList.Close();
                }
                else
                {
                    dc.Target = commandList;
                    dc.BeginDraw();
                    dc.EndDraw();
                    dc.Target = null;
                    commandList.Close();
                }

                colorMatrix.SetInput(0, commandList, true);
            }

            if (isFirst || this.strength != strength) {
                colorMatrix.Matrix = new Matrix5x4()
                {
                    M11 = (float)strength, M12 = 0, M13 = 0, M14 = 0,
                    M21 = 0, M22 = (float)strength, M23 = 0, M24 = 0,
                    M31 = 0, M32 = 0, M33 = (float)strength, M34 = 0,
                    M41 = 0, M42 = 0, M43 = 0, M44 = (float)strength,
                    M51 = 0, M52 = 0, M53 = 0, M54 = 0,
                };
            }

            isFirst = false;
            this.bounds = bounds;
            this.color = color;
            this.strength = strength;
            this.fade = fade;
            this.size = size;
            this.blur = blur;
            this.angle = angle;
            this.isAnimationActive = isAnimationActive;
            this.animationRate = animationRate;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            var dc = devices.DeviceContext;

            colorMatrix = new ColorMatrix(dc);
            disposer.Collect(colorMatrix);

            alphaMask = new AlphaMask(dc);
            disposer.Collect(alphaMask);

            composite = new Composite(dc)
            {
                InputCount = 2,
                Mode = CompositeMode.Plus
            };
            disposer.Collect(composite);

            using (var output = colorMatrix.Output)
                alphaMask.SetInput(0, output, true);
            using (var output = alphaMask.Output)
                composite.SetInput(1, output, true);

            var result = composite.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void setInput(ID2D1Image? input)
        {
            alphaMask?.SetInput(1, input, true);
            composite?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            colorMatrix?.SetInput(0, null, true);
            alphaMask?.SetInput(0, null, true);
            alphaMask?.SetInput(1, null, true);
            composite?.SetInput(0, null, true);
            composite?.SetInput(1, null, true);
        }
    }
}
