using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Audio;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Growl
{
    internal class GrowlEffectProcessor(GrowlEffect effect) : AudioEffectProcessorBase
    {
        const double TwoPi = Math.PI * 2.0;
        const float ToneShelfFrequency = 2800f;
        const float ToneShelfSlope = 0.9f;
        const double DcBlockerCutoffHz = 20.0;
        const double JitterCutoffHz = 5.0;
        const double ShimmerCutoffHz = 5.0;
        const float JitterDepthScale = 0.15f;
        const float ShimmerDepthScale = 0.3f;
        const float AsymmetryBias = 0.6f;
        const float ToneUpdateThreshold = 0.05f;

        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();
        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        sealed class ChannelState
        {
            public double LfoPhase;
            public float JitterValue;
            public float ShimmerValue;
            public uint RngState;
        }

        long currentPosition;
        readonly ChannelState leftChannel = new() { RngState = 0x9E3779B1u };
        readonly ChannelState rightChannel = new() { RngState = 0x6C62272Eu };
        readonly StereoBiQuadFilter toneFilter = new();
        float lastToneDb = float.NaN;
        bool filtersInitialized;
        float dcBlockerCoef;
        float dcInLeft, dcOutLeft, dcInRight, dcOutRight;
        float jitterAlpha, jitterNorm;
        float shimmerAlpha, shimmerNorm;

        protected override unsafe int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null) return 0;

            count -= count % 2;
            int read = Input.Read(destBuffer, offset, count);
            read -= read % 2;
            if (read <= 0) return 0;

            int hz = Hz;
            long totalPairs = Duration / 2;
            int pairs = read / 2;
            long startPair = currentPosition / 2;
            long endPair = startPair + Math.Max(0, pairs - 1);

            float driveStart = DbToLinear(effect.DriveDb.GetValue(startPair, totalPairs, hz));
            float driveEnd = DbToLinear(effect.DriveDb.GetValue(endPair, totalPairs, hz));
            float asymmetryStart = (float)effect.Asymmetry.GetValue(startPair, totalPairs, hz);
            float asymmetryEnd = (float)effect.Asymmetry.GetValue(endPair, totalPairs, hz);
            float roughnessStart = (float)(effect.Roughness.GetValue(startPair, totalPairs, hz) * 0.01);
            float roughnessEnd = (float)(effect.Roughness.GetValue(endPair, totalPairs, hz) * 0.01);
            float roughFreqStart = (float)effect.RoughnessFreq.GetValue(startPair, totalPairs, hz);
            float roughFreqEnd = (float)effect.RoughnessFreq.GetValue(endPair, totalPairs, hz);
            float toneDbStart = (float)effect.ToneDb.GetValue(startPair, totalPairs, hz);
            float toneDbEnd = (float)effect.ToneDb.GetValue(endPair, totalPairs, hz);
            float mixStart = (float)(effect.Mix.GetValue(startPair, totalPairs, hz) * 0.01);
            float mixEnd = (float)(effect.Mix.GetValue(endPair, totalPairs, hz) * 0.01);

            float averageToneDb = (toneDbStart + toneDbEnd) * 0.5f;
            EnsureFilters(hz, averageToneDb);

            float invDenominator = pairs > 1 ? 1f / (pairs - 1) : 0f;
            float coef = dcBlockerCoef;
            float jAlpha = jitterAlpha, jNorm = jitterNorm;
            float sAlpha = shimmerAlpha, sNorm = shimmerNorm;
            double invHz = 1.0 / hz;

            fixed (float* pBuf = &destBuffer[offset])
            {
                for (int i = 0; i < pairs; i++)
                {
                    float t = pairs > 1 ? i * invDenominator : 0f;
                    float drive = driveStart + (driveEnd - driveStart) * t;
                    float asymmetry = asymmetryStart + (asymmetryEnd - asymmetryStart) * t;
                    float roughness = roughnessStart + (roughnessEnd - roughnessStart) * t;
                    float roughFreq = roughFreqStart + (roughFreqEnd - roughFreqStart) * t;
                    float mix = mixStart + (mixEnd - mixStart) * t;

                    int idx = i * 2;
                    float dryLeft = pBuf[idx];
                    float dryRight = pBuf[idx + 1];

                    float modLeft = ProcessChannelModulator(leftChannel, roughness, roughFreq, jAlpha, jNorm, sAlpha, sNorm, invHz);
                    float modRight = ProcessChannelModulator(rightChannel, roughness, roughFreq, jAlpha, jNorm, sAlpha, sNorm, invHz);

                    float modulatedLeft = dryLeft * modLeft;
                    float modulatedRight = dryRight * modRight;

                    float bias = asymmetry * AsymmetryBias;
                    float tanhBias = MathF.Tanh(bias);
                    float peakPositive = MathF.Tanh(drive + bias) - tanhBias;
                    float peakNegative = tanhBias - MathF.Tanh(-drive + bias);
                    float peakMagnitude = MathF.Max(peakPositive, peakNegative);
                    float invPeak = peakMagnitude > 1e-6f ? 1f / peakMagnitude : 1f;

                    float shapedLeft = (MathF.Tanh(modulatedLeft * drive + bias) - tanhBias) * invPeak;
                    float shapedRight = (MathF.Tanh(modulatedRight * drive + bias) - tanhBias) * invPeak;

                    float dcLeft = shapedLeft - dcInLeft + coef * dcOutLeft;
                    dcInLeft = shapedLeft;
                    dcOutLeft = dcLeft;

                    float dcRight = shapedRight - dcInRight + coef * dcOutRight;
                    dcInRight = shapedRight;
                    dcOutRight = dcRight;

                    var (filteredLeft, filteredRight) = toneFilter.Transform(dcLeft, dcRight);

                    pBuf[idx] = dryLeft * (1f - mix) + filteredLeft * mix;
                    pBuf[idx + 1] = dryRight * (1f - mix) + filteredRight * mix;
                }
            }

            currentPosition += read;
            return read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float ProcessChannelModulator(
            ChannelState ch, float roughness, float roughFreq,
            float jAlpha, float jNorm, float sAlpha, float sNorm,
            double invHz)
        {
            ch.RngState ^= ch.RngState << 13;
            ch.RngState ^= ch.RngState >> 17;
            ch.RngState ^= ch.RngState << 5;
            float jitterNoise = (float)(ch.RngState & 0xFFFFFFu) * (1f / 8388607.5f) - 1f;
            ch.JitterValue = jAlpha * ch.JitterValue + (1f - jAlpha) * jitterNoise;
            float jitter = ch.JitterValue * jNorm * JitterDepthScale;

            ch.RngState ^= ch.RngState << 13;
            ch.RngState ^= ch.RngState >> 17;
            ch.RngState ^= ch.RngState << 5;
            float shimmerNoise = (float)(ch.RngState & 0xFFFFFFu) * (1f / 8388607.5f) - 1f;
            ch.ShimmerValue = sAlpha * ch.ShimmerValue + (1f - sAlpha) * shimmerNoise;
            float shimmer = ch.ShimmerValue * sNorm * ShimmerDepthScale;

            double instFreq = roughFreq * (1.0 + jitter);
            ch.LfoPhase += TwoPi * instFreq * invHz;
            if (ch.LfoPhase >= TwoPi) ch.LfoPhase -= TwoPi;
            else if (ch.LfoPhase < 0.0) ch.LfoPhase += TwoPi;

            float depth = roughness * (1f + shimmer);
            float modulator = 1f + depth * (float)Math.Cos(ch.LfoPhase);
            return modulator > 0f ? modulator : 0f;
        }

        void EnsureFilters(int hz, float toneDb)
        {
            if (!filtersInitialized)
            {
                filtersInitialized = true;

                dcBlockerCoef = (float)Math.Exp(-TwoPi * DcBlockerCutoffHz / hz);

                double jAlpha = Math.Exp(-TwoPi * JitterCutoffHz / hz);
                jitterAlpha = (float)jAlpha;
                jitterNorm = (float)Math.Sqrt(3.0 * (1.0 + jAlpha) / (1.0 - jAlpha));

                double sAlpha = Math.Exp(-TwoPi * ShimmerCutoffHz / hz);
                shimmerAlpha = (float)sAlpha;
                shimmerNorm = (float)Math.Sqrt(3.0 * (1.0 + sAlpha) / (1.0 - sAlpha));
            }

            if (float.IsNaN(lastToneDb) || MathF.Abs(toneDb - lastToneDb) > ToneUpdateThreshold)
            {
                toneFilter.SetHighShelf(hz, ToneShelfFrequency, ToneShelfSlope, toneDb);
                lastToneDb = toneDb;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float DbToLinear(double db) => (float)Math.Pow(10.0, db / 20.0);

        protected override void seek(long position)
        {
            currentPosition = position;
            Input?.Seek(position);

            ResetChannel(leftChannel, 0x9E3779B1u);
            ResetChannel(rightChannel, 0x6C62272Eu);

            dcInLeft = dcOutLeft = dcInRight = dcOutRight = 0f;
            filtersInitialized = false;
            dcBlockerCoef = 0f;
            jitterAlpha = jitterNorm = 0f;
            shimmerAlpha = shimmerNorm = 0f;
            toneFilter.Reset();
            lastToneDb = float.NaN;
        }

        static void ResetChannel(ChannelState ch, uint rngState)
        {
            ch.LfoPhase = 0.0;
            ch.JitterValue = 0f;
            ch.ShimmerValue = 0f;
            ch.RngState = rngState;
        }
    }
}
