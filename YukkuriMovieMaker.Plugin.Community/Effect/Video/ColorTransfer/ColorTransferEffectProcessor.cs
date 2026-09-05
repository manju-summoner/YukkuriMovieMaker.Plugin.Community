using System.Globalization;
using System.Numerics;
using SharpGen.Runtime;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using PixelFormat = Vortice.DCommon.PixelFormat;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    internal sealed class ColorTransferEffectProcessor(
        IGraphicsDevicesAndContext devices,
        ColorTransferEffect item) : VideoEffectProcessorBase(devices)
    {
        private const int MinimumSampleSize = 32;
        private const int MaximumSampleSize = 512;
        private const int MaximumReferenceDepth = 1;
        private const float MaximumBoundsExtent = 16384f;
        private const string BranchNamePrefix = "OutputBranch.Branch";
        private const string CurrentIndexName = "OutputBranch.CurrentIndex";

        [ThreadStatic]
        private static int _referenceDepth;

        [ThreadStatic]
        private static ColorTransferEffect? _referenceOwner;

        private readonly IGraphicsDevicesAndContext _devices = devices;
        private readonly ColorTransferEffect _item = item;
        private readonly ColorTransferAnalyzer _analyzer = new();
        private ColorTransferCustomEffect? _effect;

        private ID2D1Bitmap1? _renderBitmap;
        private ID2D1Bitmap1? _stagingBitmap;
        private int _bitmapSize;

        private ITimelineSource? _sceneSource;
        private Guid _sceneSourceId;

        private int[] _sourcePixels = [];
        private int[] _referencePixels = [];
        private int _sourceCount;
        private int _referenceCount;
        private int _referenceWidth;
        private int _referenceHeight;

        private string _branchName = string.Empty;
        private int _branchNameIndex = -1;

        private bool _isFirst = true;
        private bool _hasTransfer;
        private Parameters _parameters;
        private Mapping _mapping;
        private float _lightnessAmount = -1f;
        private float _colorAmount = -1f;
        private float _positionAmount = -1f;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null || input is null)
                return effectDescription.DrawDescription;

            if (_referenceDepth > 0 && ReferenceEquals(_referenceOwner, _item))
            {
                _hasTransfer = false;
                ApplyAmounts(0f, 0f, 0f);
                return effectDescription.DrawDescription with { Opacity = 0.0 };
            }

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var reference = _item.Reference;
            var sceneId = _item.SceneId;
            var timeOffset = _item.TimeOffset;
            var branchIndex = _item.BranchIndex;
            var mode = _item.Mode;
            var lightnessAmount = (float)(_item.LightnessAmount.GetValue(frame, length, fps) / 100.0);
            var colorAmount = (float)(_item.ColorAmount.GetValue(frame, length, fps) / 100.0);
            var maximumGain = _item.MaximumGain;
            var positionAmount = (float)(_item.PositionAmount.GetValue(frame, length, fps) / 100.0);
            var sampleSize = Math.Clamp(_item.SampleSize, MinimumSampleSize, MaximumSampleSize);

            var parameters = new Parameters(
                reference,
                sceneId,
                timeOffset,
                branchIndex,
                mode,
                lightnessAmount,
                colorAmount,
                maximumGain,
                sampleSize);

            if (lightnessAmount > 0f || colorAmount > 0f)
                UpdateTransfer(effectDescription, parameters);
            else
                _hasTransfer = false;

            ApplyMapping(effectDescription);
            ApplyAmounts(
                _hasTransfer ? lightnessAmount : 0f,
                _hasTransfer ? colorAmount : 0f,
                _hasTransfer && reference == ColorTransferReference.Scene ? positionAmount : 0f);

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        private void UpdateTransfer(EffectDescription effectDescription, in Parameters parameters)
        {
            var dc = _devices.DeviceContext;
            var sourceBounds = dc.GetImageLocalBounds(input!);

            var settingsChanged = _isFirst
                || _parameters.Reference != parameters.Reference
                || _parameters.SceneId != parameters.SceneId
                || _parameters.TimeOffset != parameters.TimeOffset
                || _parameters.BranchIndex != parameters.BranchIndex
                || _parameters.Mode != parameters.Mode
                || _parameters.MaximumGain != parameters.MaximumGain
                || _parameters.SampleSize != parameters.SampleSize;

            var reference = ResolveReference(effectDescription, parameters, out var referenceBounds);
            if (reference is null)
            {
                _hasTransfer = false;
                return;
            }

            EnsureBuffers(parameters.SampleSize);

            var changed = false;
            if (!TryCapture(dc, input!, sourceBounds, reference, referenceBounds, parameters.SampleSize, ref changed))
            {
                _hasTransfer = false;
                return;
            }

            if (!changed && !settingsChanged && _hasTransfer)
                return;

            if (!_analyzer.Analyze(
                _sourcePixels.AsSpan(0, _sourceCount),
                _referencePixels.AsSpan(0, _referenceCount),
                _referenceWidth,
                _referenceHeight,
                parameters.Mode,
                parameters.MaximumGain))
            {
                _hasTransfer = false;
                return;
            }

            _effect!.DomainMinimum = _analyzer.DomainMinimum;
            _effect.DomainScale = _analyzer.DomainScale;
            _effect.TransferLut = _analyzer.LutBytes;
            _effect.LocalDelta = _analyzer.LocalDeltaBytes;
            _hasTransfer = true;
        }

        private ID2D1Image? ResolveReference(EffectDescription effectDescription, in Parameters parameters, out RawRectF bounds)
        {
            if (parameters.Reference == ColorTransferReference.Scene)
                return ResolveScene(effectDescription, parameters, out bounds);

            ReleaseSceneSource();

            var image = ResolveBranch(effectDescription.DrawDescription, parameters.BranchIndex);
            bounds = image is null ? default : _devices.DeviceContext.GetImageLocalBounds(image);
            return image;
        }

        private ID2D1Image? ResolveScene(EffectDescription effectDescription, in Parameters parameters, out RawRectF bounds)
        {
            bounds = default;
            var sceneId = parameters.SceneId;
            if (sceneId == Guid.Empty)
            {
                ReleaseSceneSource();
                return null;
            }

            if (_referenceDepth >= MaximumReferenceDepth)
                return null;

            ISceneInfo? scene = null;
            foreach (var candidate in effectDescription.Scenes)
            {
                if (candidate.ID == sceneId)
                {
                    scene = candidate;
                    break;
                }
            }
            if (scene is null)
            {
                ReleaseSceneSource();
                return null;
            }

            var halfWidth = scene.Width * 0.5f;
            var halfHeight = scene.Height * 0.5f;
            if (!(halfWidth > 0f) || !(halfHeight > 0f))
            {
                ReleaseSceneSource();
                return null;
            }
            bounds = new RawRectF(-halfWidth, -halfHeight, halfWidth, halfHeight);

            if (_sceneSourceId != sceneId || _sceneSource is null)
            {
                ReleaseSceneSource();

                if (!scene.TryCreateVideoSource(_devices, out var source))
                    return null;

                disposer.Collect(source);
                _sceneSource = source;
                _sceneSourceId = sceneId;
            }

            var duration = scene.Duration.Time;
            var position = effectDescription.TimelinePosition.Time + parameters.TimeOffset;
            var last = duration > TimeSpan.Zero ? duration - TimeSpan.FromTicks(1) : TimeSpan.Zero;
            var time = position < TimeSpan.Zero ? TimeSpan.Zero : (position > last ? last : position);

            var previousOwner = _referenceOwner;
            _referenceOwner = _item;
            _referenceDepth++;
            try
            {
                _sceneSource.Update(time, effectDescription.Usage);
            }
            finally
            {
                _referenceDepth--;
                _referenceOwner = previousOwner;
            }

            return _sceneSource.Output;
        }

        private ID2D1Image? ResolveBranch(DrawDescription drawDescription, int branchIndex)
        {
            if (branchIndex <= 0)
                return null;
            if (drawDescription.GetCustomValue<int>(CurrentIndexName) == branchIndex)
                return null;

            if (_branchNameIndex != branchIndex)
            {
                _branchName = BranchNamePrefix + branchIndex.ToString(CultureInfo.InvariantCulture);
                _branchNameIndex = branchIndex;
            }

            return drawDescription.TryGetCustomValue<ID2D1Image>(out var image, _branchName) ? image : null;
        }

        private void ReleaseSceneSource()
        {
            if (_sceneSource is not null)
                disposer.RemoveAndDispose(ref _sceneSource);
            _sceneSourceId = Guid.Empty;
        }

        private unsafe bool TryCapture(
            ID2D1DeviceContext dc,
            ID2D1Image source,
            RawRectF sourceBounds,
            ID2D1Image reference,
            RawRectF referenceBounds,
            int sampleSize,
            ref bool changed)
        {
            if (!TryMeasure(sourceBounds, sampleSize, out var sourcePlacement)
                || !TryMeasure(referenceBounds, sampleSize, out var referencePlacement))
                return false;

            _referenceWidth = referencePlacement.Width;
            _referenceHeight = referencePlacement.Height;

            EnsureBitmaps(dc, sampleSize);

            var previousTarget = dc.Target;
            var previousTransform = dc.Transform;
            Result drawResult;
            try
            {
                dc.Target = _renderBitmap;
                dc.BeginDraw();
                try
                {
                    dc.Transform = Matrix3x2.Identity;
                    dc.Clear(new Color4(0f, 0f, 0f, 0f));
                    DrawSample(dc, source, sourcePlacement, 0f);
                    DrawSample(dc, reference, referencePlacement, sampleSize);
                }
                finally
                {
                    drawResult = dc.EndDraw();
                }
            }
            finally
            {
                dc.Transform = previousTransform;
                dc.Target = previousTarget;
                previousTarget?.Dispose();
            }
            drawResult.CheckError();

            _stagingBitmap!.CopyFromBitmap(_renderBitmap!);

            var mapped = _stagingBitmap.Map(MapOptions.Read);
            try
            {
                var basePointer = (byte*)mapped.Bits;
                ReadSample(basePointer, mapped.Pitch, sourcePlacement, 0, _sourcePixels, ref _sourceCount, ref changed);
                ReadSample(basePointer, mapped.Pitch, referencePlacement, sampleSize, _referencePixels, ref _referenceCount, ref changed);
            }
            finally
            {
                _stagingBitmap.Unmap();
            }

            return true;
        }

        private static bool TryMeasure(RawRectF bounds, int sampleSize, out Placement placement)
        {
            placement = default;

            var left = Math.Clamp(bounds.Left, -MaximumBoundsExtent, MaximumBoundsExtent);
            var top = Math.Clamp(bounds.Top, -MaximumBoundsExtent, MaximumBoundsExtent);
            var right = Math.Clamp(bounds.Right, -MaximumBoundsExtent, MaximumBoundsExtent);
            var bottom = Math.Clamp(bounds.Bottom, -MaximumBoundsExtent, MaximumBoundsExtent);

            var width = right - left;
            var height = bottom - top;
            if (!(width > 0f) || !(height > 0f))
                return false;

            var scale = Math.Min(1f, sampleSize / Math.Max(width, height));
            var sampleWidth = Math.Clamp((int)MathF.Round(width * scale), 1, sampleSize);
            var sampleHeight = Math.Clamp((int)MathF.Round(height * scale), 1, sampleSize);

            placement = new Placement(left, top, sampleWidth / width, sampleHeight / height, sampleWidth, sampleHeight);
            return true;
        }

        private static void DrawSample(ID2D1DeviceContext dc, ID2D1Image image, in Placement placement, float rowOffset)
        {
            dc.PushAxisAlignedClip(
                new RawRectF(0f, rowOffset, placement.Width, rowOffset + placement.Height),
                AntialiasMode.Aliased);
            try
            {
                dc.Transform = Matrix3x2.CreateScale(placement.ScaleX, placement.ScaleY)
                    * Matrix3x2.CreateTranslation(-placement.Left * placement.ScaleX, -placement.Top * placement.ScaleY + rowOffset);
                dc.DrawImage(image, null, null, InterpolationMode.NearestNeighbor, CompositeMode.SourceCopy);
            }
            finally
            {
                dc.Transform = Matrix3x2.Identity;
                dc.PopAxisAlignedClip();
            }
        }

        private static unsafe void ReadSample(
            byte* basePointer,
            int pitch,
            in Placement placement,
            int rowOffset,
            int[] buffer,
            ref int count,
            ref bool changed)
        {
            var pixelCount = placement.Width * placement.Height;
            if (count != pixelCount)
                changed = true;
            count = pixelCount;

            for (var row = 0; row < placement.Height; row++)
            {
                var source = new ReadOnlySpan<int>(basePointer + (nint)(row + rowOffset) * pitch, placement.Width);
                var destination = buffer.AsSpan(row * placement.Width, placement.Width);
                if (changed)
                {
                    source.CopyTo(destination);
                }
                else if (!source.SequenceEqual(destination))
                {
                    changed = true;
                    source.CopyTo(destination);
                }
            }
        }

        private void EnsureBuffers(int sampleSize)
        {
            var capacity = sampleSize * sampleSize;
            if (_sourcePixels.Length >= capacity)
                return;

            _sourcePixels = new int[capacity];
            _referencePixels = new int[capacity];
            _sourceCount = 0;
            _referenceCount = 0;
        }

        private void EnsureBitmaps(ID2D1DeviceContext dc, int sampleSize)
        {
            if (_renderBitmap is not null && _stagingBitmap is not null && _bitmapSize == sampleSize)
                return;

            disposer.RemoveAndDispose(ref _renderBitmap);
            disposer.RemoveAndDispose(ref _stagingBitmap);
            _bitmapSize = 0;

            var pixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var size = new SizeI(sampleSize, sampleSize * 2);

            _renderBitmap = dc.CreateBitmap(
                size,
                new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.Target));
            disposer.Collect(_renderBitmap);

            _stagingBitmap = dc.CreateBitmap(
                size,
                new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
            disposer.Collect(_stagingBitmap);

            _bitmapSize = sampleSize;
        }

        private void ApplyAmounts(float lightnessAmount, float colorAmount, float positionAmount)
        {
            if (_effect is null)
                return;

            if (_lightnessAmount != lightnessAmount)
            {
                _effect.LightnessAmount = lightnessAmount;
                _lightnessAmount = lightnessAmount;
            }
            if (_colorAmount != colorAmount)
            {
                _effect.ColorAmount = colorAmount;
                _colorAmount = colorAmount;
            }
            if (_positionAmount != positionAmount)
            {
                _effect.PositionAmount = positionAmount;
                _positionAmount = positionAmount;
            }
        }

        private void ApplyMapping(EffectDescription effectDescription)
        {
            if (_effect is null)
                return;

            var mapping = BuildMapping(
                effectDescription.DrawDescription,
                effectDescription.ScreenSize.Width,
                effectDescription.ScreenSize.Height);

            if (!_isFirst && _mapping == mapping)
                return;

            _effect.ItemToSceneX = mapping.RowX;
            _effect.ItemToSceneY = mapping.RowY;
            _effect.ItemToSceneW = mapping.RowW;
            _effect.SceneToGrid = mapping.SceneToGrid;
            _mapping = mapping;
        }

        private static Mapping BuildMapping(DrawDescription drawDescription, int screenWidth, int screenHeight)
        {
            var plane = (drawDescription.Invert
                ? Matrix3x2.CreateScale(-1f, 1f, drawDescription.CenterPoint)
                : Matrix3x2.Identity) * Matrix3x2.CreateScale(drawDescription.Zoom);

            var space = Matrix4x4.CreateRotationZ(MathF.PI * drawDescription.Rotation.Z / 180f)
                * Matrix4x4.CreateRotationY(MathF.PI * -drawDescription.Rotation.Y / 180f)
                * Matrix4x4.CreateRotationX(MathF.PI * -drawDescription.Rotation.X / 180f)
                * Matrix4x4.CreateTranslation(drawDescription.Draw)
                * drawDescription.Camera
                * new Matrix4x4(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, -0.001f, 0f, 0f, 0f, 1f);

            var width = screenWidth > 0 ? screenWidth : 1;
            var height = screenHeight > 0 ? screenHeight : 1;

            return new Mapping(
                new Vector4(
                    plane.M11 * space.M11 + plane.M12 * space.M21,
                    plane.M21 * space.M11 + plane.M22 * space.M21,
                    plane.M31 * space.M11 + plane.M32 * space.M21 + space.M41,
                    0f),
                new Vector4(
                    plane.M11 * space.M12 + plane.M12 * space.M22,
                    plane.M21 * space.M12 + plane.M22 * space.M22,
                    plane.M31 * space.M12 + plane.M32 * space.M22 + space.M42,
                    0f),
                new Vector4(
                    plane.M11 * space.M14 + plane.M12 * space.M24,
                    plane.M21 * space.M14 + plane.M22 * space.M24,
                    plane.M31 * space.M14 + plane.M32 * space.M24 + space.M44,
                    0f),
                new Vector4(1f / width, 1f / height, 0.5f, 0.5f));
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new ColorTransferCustomEffect(devices);
            if (!_effect.IsEnabled)
            {
                _effect.Dispose();
                _effect = null;
                return null;
            }
            disposer.Collect(_effect);

            var output = _effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            _effect?.SetInput(0, null, true);
            ReleaseSceneSource();
            _isFirst = true;
            _hasTransfer = false;
            _lightnessAmount = -1f;
            _colorAmount = -1f;
            _positionAmount = -1f;
        }

        private readonly record struct Mapping(
            Vector4 RowX,
            Vector4 RowY,
            Vector4 RowW,
            Vector4 SceneToGrid);

        private readonly record struct Placement(
            float Left,
            float Top,
            float ScaleX,
            float ScaleY,
            int Width,
            int Height);

        private readonly record struct Parameters(
            ColorTransferReference Reference,
            Guid SceneId,
            TimeSpan TimeOffset,
            int BranchIndex,
            ColorTransferMode Mode,
            float LightnessAmount,
            float ColorAmount,
            double MaximumGain,
            int SampleSize);
    }
}
