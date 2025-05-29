using NAudio.Dsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer
{
    public class EqualizerEffectProcessor(EqualizerEffect effect) : AudioEffectProcessorBase()
    {
        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();

        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        const float q = 1.41f;
        readonly Band[] bands = [
            new (32, q),
            new (64, q),
            new (125, q),
            new (250, q),
            new (500, q),
            new (1000, q),
            new (2000, q),
            new (4000, q),
            new (8000, q),
            new (16000, q),
            ];
        readonly BiQuadFilter[] filters = new BiQuadFilter[10];
        readonly float[] gains = new float[10];
        long position;

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null)
                return 0;
            count -= count % 2;
            var read = Input.Read(destBuffer, offset, count) / 2;
            for (int i = 0; i < read; i++)
            {
                var samplePosition = position + i * 2;
                var newGains = GetCurrentGains(samplePosition);
                if (!gains.SequenceEqual(newGains) || filters[0] is null)
                {

                    for (int j = 0; j < filters.Length; j++)
                    {
                        gains[j] = newGains[j];
                        if (filters[j] is null)
                            filters[j] = BiQuadFilter.PeakingEQ(Hz, bands[j].Frequency, bands[j].Q, gains[j]);
                        else
                            filters[j].SetPeakingEq(Hz, bands[j].Frequency, bands[j].Q, gains[j]);
                    }
                }

                for (int j = 0; j < filters.Length; j++)
                {
                    destBuffer[offset + i * 2] = filters[j].Transform(destBuffer[offset + i * 2]);
                    destBuffer[offset + i * 2 + 1] = filters[j].Transform(destBuffer[offset + i * 2 + 1]);
                }
            }
            position += read * 2;
            return read * 2;
        }

        float[] GetCurrentGains(long position)
        {
            var current = position / 2;
            var total = Duration / 2;
            return [.. new[] { effect.B32, effect.B64, effect.B125, effect.B250, effect.B500, effect.B1k, effect.B2k, effect.B4k, effect.B8k, effect.B16k }.Select(a => (float)a.GetValue(current, total, Hz))];
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            this.position = position;
        }

        record class Band(float Frequency, float Q);
    }
}
