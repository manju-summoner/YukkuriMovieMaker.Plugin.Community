using NAudio.Dsp;
using System;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    public class Equalizer2EffectProcessor : AudioEffectProcessorBase
    {
        readonly Equalizer2Effect effect;
        BiQuadFilter[] filtersL = [];
        BiQuadFilter[] filtersR = [];
        FilterType[] types = [];
        float[] frequencies = [];
        float[] gains = [];
        float[] qs = [];
        long position;

        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();

        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        public Equalizer2EffectProcessor(Equalizer2Effect effect)
        {
            this.effect = effect;
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null)
                return 0;

            count -= count % 2;
            var read = Input.Read(destBuffer, offset, count) / 2;

            for (int i = 0; i < read; i++)
            {
                var samplePosition = position + i * 2;
                UpdateFilters(samplePosition);

                for (int j = 0; j < filtersL.Length; j++)
                {
                    destBuffer[offset + i * 2] = filtersL[j].Transform(destBuffer[offset + i * 2]);
                    destBuffer[offset + i * 2 + 1] = filtersR[j].Transform(destBuffer[offset + i * 2 + 1]);
                }
            }

            position += read * 2;
            return read * 2;
        }

        void UpdateFilters(long samplePosition)
        {
            var bands = effect.Bands;
            if (bands is null)
                return;

            if (filtersL.Length != bands.Count)
            {
                filtersL = new BiQuadFilter[bands.Count];
                filtersR = new BiQuadFilter[bands.Count];
                types = new FilterType[bands.Count];
                frequencies = new float[bands.Count];
                gains = new float[bands.Count];
                qs = new float[bands.Count];
            }

            var current = samplePosition / 2;
            var total = Duration / 2;

            for (int i = 0; i < bands.Count; i++)
            {
                var band = bands[i];
                var type = band.FilterType;
                var frequency = (float)band.Frequency.GetValue(current, total, Hz);
                var gain = (float)band.Gain.GetValue(current, total, Hz);
                var q = (float)band.Q.GetValue(current, total, Hz);

                if (filtersL[i] is null || types[i] != type || frequencies[i] != frequency || gains[i] != gain || qs[i] != q)
                {
                    types[i] = type;
                    frequencies[i] = frequency;
                    gains[i] = gain;
                    qs[i] = q;

                    filtersL[i] = CreateFilter(type, Hz, frequency, q, gain);
                    filtersR[i] = CreateFilter(type, Hz, frequency, q, gain);
                }
            }
        }

        BiQuadFilter CreateFilter(FilterType type, int sampleRate, float frequency, float q, float gain)
        {
            return type switch
            {
                FilterType.LowPass => BiQuadFilter.LowPassFilter(sampleRate, frequency, q),
                FilterType.HighPass => BiQuadFilter.HighPassFilter(sampleRate, frequency, q),
                FilterType.BandPass => BiQuadFilter.BandPassFilterConstantSkirtGain(sampleRate, frequency, q),
                FilterType.AllPass => BiQuadFilter.AllPassFilter(sampleRate, frequency, q),
                FilterType.Notch => BiQuadFilter.NotchFilter(sampleRate, frequency, q),
                FilterType.LowShelf => BiQuadFilter.LowShelf(sampleRate, frequency, q, gain),
                FilterType.HighShelf => BiQuadFilter.HighShelf(sampleRate, frequency, q, gain),
                FilterType.PeakingEQ => BiQuadFilter.PeakingEQ(sampleRate, frequency, q, gain),
                _ => BiQuadFilter.PeakingEQ(sampleRate, frequency, q, gain),
            };
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            this.position = position;
        }
    }
}
