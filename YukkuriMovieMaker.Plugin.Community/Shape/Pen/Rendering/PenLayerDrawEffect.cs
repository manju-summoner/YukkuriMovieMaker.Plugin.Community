using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    internal sealed class PenLayerDrawEffect : IDisposable
    {
        static readonly Matrix4x4 perspectiveMatrix = new(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, -0.001f,
            0f, 0f, 0f, 1f);

        readonly IGraphicsDevicesAndContext devices;
        readonly DisposeCollector disposer = new();
        readonly Flood emptyEffect;
        readonly Crop emptyCropEffect;
        readonly AffineTransform2D zoomEffect;
        readonly Crop cropEffect;
        readonly Transform3D renderEffect;
        readonly ColorMatrix opacityEffect;
        readonly ID2D1Image renderOutput;

        ID2D1Image? input;
        bool isFirstUpdate = true;
        bool isEmpty;
        RawRectF bounds = new(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);
        Vector3 draw;
        Vector2 zoom;
        Vector3 rotation;
        Matrix4x4 camera;
        Vector2 centerPoint;
        AffineTransform2DInterpolationMode zoomInterpolationMode;
        double opacity = 100;
        bool isInverted;

        public ID2D1Image Output { get; }

        public PenLayerDrawEffect(IGraphicsDevicesAndContext devices)
        {
            this.devices = devices;

            emptyEffect = new Flood(devices.DeviceContext);
            disposer.Collect(emptyEffect);
            emptyCropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(emptyCropEffect);
            using (var image = emptyEffect.Output)
                emptyCropEffect.SetInput(0, image, true);

            zoomEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(zoomEffect);
            cropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);
            renderEffect = new Transform3D(devices.DeviceContext);
            disposer.Collect(renderEffect);
            opacityEffect = new ColorMatrix(devices.DeviceContext);
            disposer.Collect(opacityEffect);

            using (var image = zoomEffect.Output)
                cropEffect.SetInput(0, image, true);
            using (var image = cropEffect.Output)
                renderEffect.SetInput(0, image, true);
            using (var image = renderEffect.Output)
                opacityEffect.SetInput(0, image, true);

            renderOutput = renderEffect.Output;
            disposer.Collect(renderOutput);

            var output = opacityEffect.Output;
            disposer.Collect(output);
            Output = output;
        }

        public void SetInput(ID2D1Image image)
        {
            if (ReferenceEquals(input, image))
                return;

            input = image;
            zoomEffect.SetInput(0, image, true);
            isEmpty = false;
        }

        public void Update(DrawDescription description)
        {
            if (input is null)
                return;

            var draw = description.Draw;
            var rotation = description.Rotation;
            var zoom = description.Zoom;
            var camera = description.Camera;
            var zoomInterpolationMode = description.ZoomInterpolationMode.ToTransform2D();
            var isInverted = description.Invert;
            var centerPoint = description.CenterPoint;
            var opacity = description.Opacity;
            var isEmpty = zoom.X == 0f || zoom.Y == 0f || opacity == 0;

            if (this.isEmpty != isEmpty)
            {
                if (isEmpty)
                {
                    using var image = emptyCropEffect.Output;
                    zoomEffect.SetInput(0, image, true);
                }
                else
                {
                    zoomEffect.SetInput(0, input, true);
                }
            }

            if (isEmpty)
            {
                var localBounds = devices.DeviceContext.GetImageLocalBounds(input);
                if (!bounds.Equals(localBounds))
                    emptyCropEffect.Rectangle = new Vector4(localBounds.Left, localBounds.Top, localBounds.Right, localBounds.Bottom);
                bounds = localBounds;
            }

            if (isFirstUpdate || this.zoom != zoom || this.zoomInterpolationMode != zoomInterpolationMode || this.isInverted != isInverted || this.centerPoint != centerPoint)
            {
                zoomEffect.TransformMatrix = (isInverted ? Matrix3x2.CreateScale(-1f, 1f, centerPoint) : Matrix3x2.Identity) * Matrix3x2.CreateScale(zoom);
                zoomEffect.InterPolationMode = zoomInterpolationMode;
            }

            if (isFirstUpdate || this.zoom != zoom || this.rotation != rotation || this.draw != draw || this.camera != camera)
                renderEffect.TransformMatrix = CreateWorldMatrix(rotation, draw, camera) * perspectiveMatrix;

            SafeTransform3DHelper.Apply(devices.DeviceContext, cropEffect, renderOutput);

            if (isFirstUpdate || this.opacity != opacity)
                opacityEffect.Matrix = new Matrix5x4 { M11 = 1f, M22 = 1f, M33 = 1f, M44 = (float)opacity };

            isFirstUpdate = false;
            this.isEmpty = isEmpty;
            this.draw = draw;
            this.zoom = zoom;
            this.rotation = rotation;
            this.camera = camera;
            this.zoomInterpolationMode = zoomInterpolationMode;
            this.isInverted = isInverted;
            this.centerPoint = centerPoint;
            this.opacity = opacity;
        }

        public void ClearInput()
        {
            input = null;
            zoomEffect.SetInput(0, null, true);
            cropEffect.SetInput(0, null, true);
            renderEffect.SetInput(0, null, true);
            opacityEffect.SetInput(0, null, true);
            emptyCropEffect.SetInput(0, null, true);
        }

        static Matrix4x4 CreateWorldMatrix(Vector3 rotation, Vector3 draw, Matrix4x4 camera)
            => Matrix4x4.CreateRotationZ(MathF.PI * rotation.Z / 180f)
            * Matrix4x4.CreateRotationY(MathF.PI * -rotation.Y / 180f)
            * Matrix4x4.CreateRotationX(MathF.PI * -rotation.X / 180f)
            * Matrix4x4.CreateTranslation(draw)
            * camera;

        public void Dispose() => disposer.Dispose();
    }
}
