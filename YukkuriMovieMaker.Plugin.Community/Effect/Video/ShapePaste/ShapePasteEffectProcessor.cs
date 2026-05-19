using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Shape;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShapePaste
{
    internal class ShapePasteEffectProcessor : IVideoEffectProcessor
    {
        const float PerspectiveDistance = 1000f;
        const float MinimumScale = 1e-5f;

        readonly DisposeCollector disposer = new();
        readonly IGraphicsDevicesAndContext devices;
        readonly ShapePasteEffect item;

        readonly ColorMatrix shapeOpacity;
        readonly AffineTransform2D shapeTransform;
        readonly ID2D1Image shapeTransformOutput;
        readonly Composite composite;
        readonly ID2D1Image emptyImage;

        IShapeSource? shapeSource;
        ID2D1Image? input;

        public ID2D1Image Output { get; }

        public ShapePasteEffectProcessor(IGraphicsDevicesAndContext devices, ShapePasteEffect item)
        {
            this.devices = devices;
            this.item = item;

            shapeOpacity = new ColorMatrix(devices.DeviceContext);
            disposer.Collect(shapeOpacity);

            shapeTransform = new AffineTransform2D(devices.DeviceContext)
            {
                InterPolationMode = AffineTransform2DInterpolationMode.Linear,
            };
            disposer.Collect(shapeTransform);

            shapeTransformOutput = shapeTransform.Output;
            disposer.Collect(shapeTransformOutput);

            composite = new Composite(devices.DeviceContext);
            disposer.Collect(composite);

            Output = composite.Output;
            disposer.Collect(Output);

            var emptyFlood = new Flood(devices.DeviceContext) { Color = new Color4(0f, 0f, 0f, 0f) };
            disposer.Collect(emptyFlood);
            disposer.Collect(emptyFlood.Output);

            var emptyCrop = new Crop(devices.DeviceContext) { Rectangle = new Vector4(0f, 0f, 1f, 1f) };
            emptyCrop.SetInput(0, emptyFlood.Output, true);
            disposer.Collect(emptyCrop);

            emptyImage = emptyCrop.Output;
            disposer.Collect(emptyImage);
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            UpdateShapeSource(effectDescription, frame, length, fps);
            ConfigureComposite();

            return effectDescription.DrawDescription;
        }

        void UpdateShapeSource(EffectDescription effectDescription, int frame, int length, int fps)
        {
            var param = item.ShapeParameter;
            if (param is null)
                return;

            DisposeShapeSource();

            var sharedData = param.GetSharedData();
            var liveParam = ShapeFactory.GetPlugin(item.ShapeType).CreateShapeParameter(sharedData);
            shapeSource = liveParam.CreateShapeSource(devices);
            shapeSource.Update(effectDescription);

            var x = (float)item.X.GetValue(frame, length, fps);
            var y = (float)item.Y.GetValue(frame, length, fps);
            var z = (float)item.Z.GetValue(frame, length, fps);
            var zoom = item.Zoom.GetValue(frame, length, fps) / 100.0;

            var perspectiveScale = ComputePerspectiveScale(z);

            float finalScaleX;
            float finalScaleY;
            float offsetX = x;
            float offsetY = y;

            if (item.IsSizeTrackingEnabled && liveParam is IResizableShapeParameter resizable && input is not null)
            {
                var bounds = devices.DeviceContext.GetImageLocalBounds(shapeSource.Output);
                var baseShapeWidth = Math.Max(1.0, bounds.Right - bounds.Left);
                var baseShapeHeight = Math.Max(1.0, bounds.Bottom - bounds.Top);

                var inputBounds = devices.DeviceContext.GetImageLocalBounds(input);
                var inputWidth = inputBounds.Right - inputBounds.Left;
                var inputHeight = inputBounds.Bottom - inputBounds.Top;

                if (float.IsFinite(inputWidth) && float.IsFinite(inputHeight))
                {
                    var leftMargin = item.Left.GetValue(frame, length, fps);
                    var rightMargin = item.Right.GetValue(frame, length, fps);
                    var topMargin = item.Top.GetValue(frame, length, fps);
                    var bottomMargin = item.Bottom.GetValue(frame, length, fps);

                    var baseHalfW = (baseShapeWidth / 2.0) * zoom * perspectiveScale;
                    var baseL = x - baseHalfW;
                    var baseR = x + baseHalfW;

                    var targetL = item.PinLeft ? (-inputWidth / 2.0 - leftMargin) : baseL;
                    var targetR = item.PinRight ? (inputWidth / 2.0 + rightMargin) : baseR;
                    var targetWidth = Math.Clamp(Math.Max(targetR - targetL, 1.0), 1.0, YMM4Constants.MaximumBitmapSize);

                    var baseHalfH = (baseShapeHeight / 2.0) * zoom * perspectiveScale;
                    var baseT = y - baseHalfH;
                    var baseB = y + baseHalfH;

                    var targetT = item.PinTop ? (-inputHeight / 2.0 - topMargin) : baseT;
                    var targetB = item.PinBottom ? (inputHeight / 2.0 + bottomMargin) : baseB;
                    var targetHeight = Math.Clamp(Math.Max(targetB - targetT, 1.0), 1.0, YMM4Constants.MaximumBitmapSize);

                    resizable.Resize(targetWidth / baseShapeWidth, targetHeight / baseShapeHeight);
                    shapeSource.Update(effectDescription);

                    offsetX = (float)((targetL + targetR) / 2.0);
                    offsetY = (float)((targetT + targetB) / 2.0);
                    finalScaleX = 1f;
                    finalScaleY = 1f;
                }
                else
                {
                    finalScaleX = finalScaleY = ComputeUniformScale(zoom, perspectiveScale);
                }
            }
            else
            {
                finalScaleX = finalScaleY = ComputeUniformScale(zoom, perspectiveScale);
            }

            if (item.InvertX) finalScaleX *= -1f;
            if (item.InvertY) finalScaleY *= -1f;

            UpdateShapeTransform(frame, length, fps, offsetX, offsetY, finalScaleX, finalScaleY);

            shapeOpacity.SetInput(0, shapeSource.Output, true);
            shapeTransform.SetInput(0, shapeOpacity.Output, true);
        }

        static float ComputeUniformScale(double zoom, double perspectiveScale)
            => (float)Math.Max(zoom * perspectiveScale, MinimumScale);

        static double ComputePerspectiveScale(float z)
        {
            if (z >= PerspectiveDistance)
                return MinimumScale;

            return Math.Max(PerspectiveDistance / (PerspectiveDistance - z), MinimumScale);
        }

        void UpdateShapeTransform(int frame, int length, int fps, float offsetX, float offsetY, float scaleX, float scaleY)
        {
            if (shapeSource is null)
                return;

            var opacity = (float)(item.Opacity.GetValue(frame, length, fps) / 100.0);
            shapeOpacity.Matrix = new Matrix5x4
            {
                M11 = 1f,
                M22 = 1f,
                M33 = 1f,
                M44 = opacity,
            };

            var rotation = (float)(item.Rotation.GetValue(frame, length, fps) * Math.PI / 180.0);
            shapeTransform.TransformMatrix =
                Matrix3x2.CreateScale(scaleX, scaleY) *
                Matrix3x2.CreateRotation(rotation) *
                Matrix3x2.CreateTranslation(offsetX, offsetY);
        }

        void ConfigureComposite()
        {
            var validInput = input ?? emptyImage;
            var shapeOutput = shapeSource is not null ? shapeTransformOutput : emptyImage;

            switch (item.DisplayMode)
            {
                case ShapeDisplayMode.Replace:
                    composite.Mode = CompositeMode.SourceOver;
                    composite.SetInput(0, emptyImage, true);
                    composite.SetInput(1, shapeOutput, true);
                    break;
                case ShapeDisplayMode.InsideArea:
                    composite.Mode = CompositeMode.SourceIn;
                    composite.SetInput(0, validInput, true);
                    composite.SetInput(1, shapeOutput, true);
                    break;
                case ShapeDisplayMode.AboveArea:
                    composite.Mode = CompositeMode.SourceAtop;
                    composite.SetInput(0, validInput, true);
                    composite.SetInput(1, shapeOutput, true);
                    break;
                default:
                    composite.Mode = CompositeMode.SourceOver;
                    if (item.IsBack)
                    {
                        composite.SetInput(0, shapeOutput, true);
                        composite.SetInput(1, validInput, true);
                    }
                    else
                    {
                        composite.SetInput(0, validInput, true);
                        composite.SetInput(1, shapeOutput, true);
                    }
                    break;
            }
        }

        public void SetInput(ID2D1Image? input)
            => this.input = input;

        public void ClearInput()
        {
            input = null;
            DisposeShapeSource();
        }

        void DisposeShapeSource()
        {
            if (shapeSource is null)
                return;

            shapeOpacity.SetInput(0, null, true);
            shapeTransform.SetInput(0, null, true);
            shapeSource.Dispose();
            shapeSource = null;
        }

        public void Dispose()
        {
            composite.SetInput(0, null, true);
            composite.SetInput(1, null, true);
            DisposeShapeSource();
            disposer.Dispose();
        }
    }
}
