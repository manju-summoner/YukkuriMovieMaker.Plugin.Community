using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using D2DEffects = Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Shape.StringSpectrum
{
    internal sealed class StringSpectrumSource : IAudioSpectrumSource
    {
        private const int MaxModes = StringSpectrumCustomEffect.MaxModes;

        private readonly DisposeCollector _disposer = new();
        private readonly StringSpectrumParameter _parameter;
        private readonly float[] _values = new float[MaxModes];
        private readonly byte[] _modeBytes = new byte[StringSpectrumCustomEffect.ModeByteSize];

        private readonly D2DEffects.Flood _flood;
        private readonly D2DEffects.Crop _crop;
        private StringSpectrumCustomEffect? _effect;

        private bool _isFirst = true;
        private Vector4 _cropRect;
        private Parameters _parameters;

        public ID2D1Image Output { get; }

        public StringSpectrumSource(IGraphicsDevicesAndContext devices, StringSpectrumParameter parameter)
        {
            _parameter = parameter;

            _flood = new D2DEffects.Flood(devices.DeviceContext);
            _disposer.Collect(_flood);
            _crop = new D2DEffects.Crop(devices.DeviceContext);
            _disposer.Collect(_crop);
            _flood.Color = new Vector4(0f, 0f, 0f, 0f);

            _effect = new StringSpectrumCustomEffect(devices);
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
            var color = _parameter.StringColor;

            var available = SpectrumResampler.Resample(spectrum, _values);
            var width = _parameter.StringWidth.GetValue(frame, length, fps);
            var visible = Math.Max((int)(width / 3.0), 1);
            var modes = Math.Clamp(Math.Min(available, Math.Min(_parameter.ModeLimit, visible)), 0, MaxModes);

            var seconds = desc.ItemPosition.Time.TotalSeconds;
            var baseFrequency = _parameter.BaseFrequency.GetValue(frame, length, fps);

            var scale = 0.0;
            if (modes > 0)
            {
                var magnitude = 0.0;
                for (var i = 0; i < modes; i++)
                    magnitude += Math.Abs(_values[i]);

                var normalize = 1.0 / Math.Sqrt(modes);
                scale = normalize / Math.Max(magnitude * normalize, 1.0);
            }

            var amplitudes = MemoryMarshal.Cast<byte, float>(_modeBytes.AsSpan());
            for (var i = 0; i < modes; i++)
            {
                var turn = seconds * baseFrequency * (i + 1);
                amplitudes[i] = (float)(_values[i] * Math.Cos(2.0 * Math.PI * (turn - Math.Floor(turn))) * scale);
            }
            amplitudes[modes..].Clear();

            var parameters = new Parameters(
                (float)width,
                (float)_parameter.Amplitude.GetValue(frame, length, fps),
                modes,
                (float)_parameter.Thickness.GetValue(frame, length, fps),
                color.R / 255f,
                color.G / 255f,
                color.B / 255f,
                color.A / 255f);

            if (_isFirst || _parameters.Width != parameters.Width)
                _effect.Width = parameters.Width;
            if (_isFirst || _parameters.Amplitude != parameters.Amplitude)
                _effect.Amplitude = parameters.Amplitude;
            if (_isFirst || _parameters.ModeCount != parameters.ModeCount)
                _effect.ModeCount = parameters.ModeCount;
            if (_isFirst || _parameters.Thickness != parameters.Thickness)
                _effect.Thickness = parameters.Thickness;
            if (_isFirst || _parameters.ColorR != parameters.ColorR)
                _effect.ColorR = parameters.ColorR;
            if (_isFirst || _parameters.ColorG != parameters.ColorG)
                _effect.ColorG = parameters.ColorG;
            if (_isFirst || _parameters.ColorB != parameters.ColorB)
                _effect.ColorB = parameters.ColorB;
            if (_isFirst || _parameters.ColorA != parameters.ColorA)
                _effect.ColorA = parameters.ColorA;

            _effect.Modes = _modeBytes;

            var halfWidth = parameters.Width * 0.5f + 1f;
            var steepest = parameters.Amplitude * Math.PI * modes / width;
            var halfHeight = (float)(parameters.Amplitude + (parameters.Thickness * 0.5 + 2.0) * Math.Sqrt(1.0 + steepest * steepest));
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
            float Width,
            float Amplitude,
            float ModeCount,
            float Thickness,
            float ColorR,
            float ColorG,
            float ColorB,
            float ColorA);
    }
}
