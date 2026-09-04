using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Shape.Spectrum;
using YukkuriMovieMaker.Plugin.Shape;
using D2DEffects = Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Shape.RippleSpectrum
{
    internal sealed class RippleSpectrumSource : IAudioSpectrumSource
    {
        private readonly DisposeCollector _disposer = new();
        private readonly RippleSpectrumParameter _parameter;
        private readonly byte[] _valueBytes = new byte[RippleSpectrumCustomEffect.ValueByteSize];

        private readonly D2DEffects.Flood _flood;
        private readonly D2DEffects.Crop _crop;
        private RippleSpectrumCustomEffect? _effect;

        private bool _isFirst = true;
        private Vector4 _cropRect;
        private Parameters _parameters;

        public ID2D1Image Output { get; }

        public RippleSpectrumSource(IGraphicsDevicesAndContext devices, RippleSpectrumParameter parameter)
        {
            _parameter = parameter;

            _flood = new D2DEffects.Flood(devices.DeviceContext);
            _disposer.Collect(_flood);
            _crop = new D2DEffects.Crop(devices.DeviceContext);
            _disposer.Collect(_crop);
            _flood.Color = new Vector4(0f, 0f, 0f, 0f);

            _effect = new RippleSpectrumCustomEffect(devices);
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
            var color = _parameter.RippleColor;

            var count = SpectrumResampler.Resample(spectrum, MemoryMarshal.Cast<byte, float>(_valueBytes.AsSpan()));

            var reach = _parameter.Reach.GetValue(frame, length, fps);
            var maxThickness = _parameter.MaxThickness.GetValue(frame, length, fps);
            var minThickness = Math.Min(_parameter.MinThickness.GetValue(frame, length, fps), maxThickness);
            var seconds = desc.ItemPosition.Time.TotalSeconds;
            var travel = seconds * _parameter.Speed.GetValue(frame, length, fps);

            var window = 0;
            if (count > 1)
            {
                var overlap = (int)Math.Ceiling(maxThickness * count / (2.0 * reach)) + 1;
                window = Math.Clamp(overlap, 1, Math.Min(8, count - 1));
            }

            var parameters = new Parameters(
                (float)_parameter.InnerRadius.GetValue(frame, length, fps),
                (float)reach,
                (float)minThickness,
                (float)maxThickness,
                window,
                (float)(_parameter.Decay.GetValue(frame, length, fps) / 100.0),
                (float)(_parameter.ValueFollow.GetValue(frame, length, fps) / 100.0),
                color.R / 255f,
                color.G / 255f,
                color.B / 255f,
                color.A / 255f);

            if (_isFirst || _parameters.InnerRadius != parameters.InnerRadius)
                _effect.InnerRadius = parameters.InnerRadius;
            if (_isFirst || _parameters.Reach != parameters.Reach)
                _effect.Reach = parameters.Reach;
            if (_isFirst || _parameters.MinThickness != parameters.MinThickness)
                _effect.MinThickness = parameters.MinThickness;
            if (_isFirst || _parameters.MaxThickness != parameters.MaxThickness)
                _effect.MaxThickness = parameters.MaxThickness;
            if (_isFirst || _parameters.Window != parameters.Window)
                _effect.Window = parameters.Window;
            if (_isFirst || _parameters.Decay != parameters.Decay)
                _effect.Decay = parameters.Decay;
            if (_isFirst || _parameters.ValueFollow != parameters.ValueFollow)
                _effect.ValueFollow = parameters.ValueFollow;
            if (_isFirst || _parameters.ColorR != parameters.ColorR)
                _effect.ColorR = parameters.ColorR;
            if (_isFirst || _parameters.ColorG != parameters.ColorG)
                _effect.ColorG = parameters.ColorG;
            if (_isFirst || _parameters.ColorB != parameters.ColorB)
                _effect.ColorB = parameters.ColorB;
            if (_isFirst || _parameters.ColorA != parameters.ColorA)
                _effect.ColorA = parameters.ColorA;

            _effect.TravelOffset = (float)(travel - Math.Floor(travel));
            _effect.ValueCount = count;
            _effect.Values = _valueBytes;

            var extent = parameters.InnerRadius + parameters.Reach + parameters.MaxThickness * 0.5f + 2f;
            var cropRect = new Vector4(-extent, -extent, extent, extent);
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
            float InnerRadius,
            float Reach,
            float MinThickness,
            float MaxThickness,
            float Window,
            float Decay,
            float ValueFollow,
            float ColorR,
            float ColorG,
            float ColorB,
            float ColorA);
    }
}
