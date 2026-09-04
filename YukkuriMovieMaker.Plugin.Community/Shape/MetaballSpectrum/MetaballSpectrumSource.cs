using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using D2DEffects = Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Shape.MetaballSpectrum
{
    internal sealed class MetaballSpectrumSource : IAudioSpectrumSource
    {
        private const int WindowLimit = MetaballSpectrumCustomEffect.MaxWindow;

        private readonly DisposeCollector _disposer = new();
        private readonly MetaballSpectrumParameter _parameter;
        private readonly byte[] _valueBytes = new byte[MetaballSpectrumCustomEffect.ValueByteSize];

        private readonly D2DEffects.Flood _flood;
        private readonly D2DEffects.Crop _crop;
        private MetaballSpectrumCustomEffect? _effect;

        private bool _isFirst = true;
        private Vector4 _cropRect;
        private Parameters _parameters;

        public ID2D1Image Output { get; }

        public MetaballSpectrumSource(IGraphicsDevicesAndContext devices, MetaballSpectrumParameter parameter)
        {
            _parameter = parameter;

            _flood = new D2DEffects.Flood(devices.DeviceContext);
            _disposer.Collect(_flood);
            _crop = new D2DEffects.Crop(devices.DeviceContext);
            _disposer.Collect(_crop);
            _flood.Color = new Vector4(0f, 0f, 0f, 0f);

            _effect = new MetaballSpectrumCustomEffect(devices);
            _disposer.Collect(_effect);

            if (!_effect.IsEnabled)
            {
                _disposer.RemoveAndDispose(ref _effect);
                _crop.Rectangle = new Vector4(0f, 0f, 0f, 0f);
                using (var output = _flood.Output)
                    _crop.SetInput(0, output, true);
            }
            else
            {
                using (var output = _flood.Output)
                    _effect.SetInput(0, output, true);
                using (var output = _effect.Output)
                    _crop.SetInput(0, output, true);
            }

            var result = _crop.Output;
            _disposer.Collect(result);
            Output = result;
        }

        public void Update(TimelineItemSourceDescription desc, float[] spectrum)
        {
            if (_effect is null)
                return;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var color = _parameter.MetaballColor;

            var count = SpectrumResampler.Resample(spectrum, MemoryMarshal.Cast<byte, float>(_valueBytes.AsSpan()));

            var fieldWidth = _parameter.FieldWidth.GetValue(frame, length, fps);
            var radius = _parameter.BlobRadius.GetValue(frame, length, fps);

            var window = 0;
            if (count > 0)
            {
                var columnWidth = fieldWidth / count;
                radius = Math.Min(radius, (WindowLimit - 0.5) * columnWidth);
                window = Math.Clamp((int)Math.Ceiling(0.5 + radius / columnWidth), 1, WindowLimit);
            }

            var parameters = new Parameters(
                (float)fieldWidth,
                (float)_parameter.FieldHeight.GetValue(frame, length, fps),
                (float)radius,
                (float)(_parameter.Threshold.GetValue(frame, length, fps) / 100.0),
                window,
                _parameter.IsBipolar ? 1f : 0f,
                color.R / 255f,
                color.G / 255f,
                color.B / 255f,
                color.A / 255f);

            if (_isFirst || _parameters.FieldWidth != parameters.FieldWidth)
                _effect.FieldWidth = parameters.FieldWidth;
            if (_isFirst || _parameters.FieldHeight != parameters.FieldHeight)
                _effect.FieldHeight = parameters.FieldHeight;
            if (_isFirst || _parameters.BlobRadius != parameters.BlobRadius)
                _effect.BlobRadius = parameters.BlobRadius;
            if (_isFirst || _parameters.Threshold != parameters.Threshold)
                _effect.Threshold = parameters.Threshold;
            if (_isFirst || _parameters.Window != parameters.Window)
                _effect.Window = parameters.Window;
            if (_isFirst || _parameters.Bipolar != parameters.Bipolar)
                _effect.Bipolar = parameters.Bipolar;
            if (_isFirst || _parameters.ColorR != parameters.ColorR)
                _effect.ColorR = parameters.ColorR;
            if (_isFirst || _parameters.ColorG != parameters.ColorG)
                _effect.ColorG = parameters.ColorG;
            if (_isFirst || _parameters.ColorB != parameters.ColorB)
                _effect.ColorB = parameters.ColorB;
            if (_isFirst || _parameters.ColorA != parameters.ColorA)
                _effect.ColorA = parameters.ColorA;

            _effect.ValueCount = count;
            _effect.Values = _valueBytes;

            var margin = parameters.BlobRadius + 2f;
            var halfWidth = parameters.FieldWidth * 0.5f + margin;
            var halfHeight = parameters.FieldHeight * 0.5f + margin;
            var cropRect = new Vector4(-halfWidth, -halfHeight, halfWidth, halfHeight);
            if (_isFirst || _cropRect != cropRect)
            {
                _crop.Rectangle = cropRect;
                _cropRect = cropRect;
            }

            _parameters = parameters;
            _isFirst = false;
        }

        public void Dispose()
        {
            _crop.SetInput(0, null, true);
            _effect?.SetInput(0, null, true);
            _disposer.DisposeAndClear();
        }

        private readonly record struct Parameters(
            float FieldWidth,
            float FieldHeight,
            float BlobRadius,
            float Threshold,
            float Window,
            float Bipolar,
            float ColorR,
            float ColorG,
            float ColorB,
            float ColorA);
    }
}
