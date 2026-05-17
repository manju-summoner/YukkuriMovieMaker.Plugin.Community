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
        const double ZcLowpassCutoffHz = 500.0;
        const double ZcLowpassQ = 0.7071067811865476;
        const float ShimmerDepthScale = 0.3f;
        const float AsymmetryBias = 0.6f;
        const float ToneUpdateThreshold = 0.05f;
        const float RoughnessFreqUpdateThreshold = 0.5f;
        const float SubharmonicMix = 0.5f;
        const double MinFundamentalHz = 60.0;
        const double MaxFundamentalHz = 800.0;
        const double EnvelopeAttackMs = 5.0;
        const double EnvelopeReleaseMs = 50.0;

        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();
        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        sealed class ChannelState
        {
            public double ZcZ1, ZcZ2;
            public double LastFiltered;
            public double SubPhase;
            public double SubPhaseInc;
            public int ZcCounter;
            public float ShimmerValue;
            public uint RngState;
            public float Envelope;
        }

        long currentPosition;
        readonly ChannelState leftChannel = new() { RngState = 0x9E3779B1u };
        readonly ChannelState rightChannel = new() { RngState = 0x6C62272Eu };
        readonly StereoBiQuadFilter toneFilter = new();
        float lastToneDb = float.NaN;
        float lastRoughnessFreq = float.NaN;
        bool filtersInitialized;
        float dcBlockerCoef;
        float dcInLeft, dcOutLeft, dcInRight, dcOutRight;
        double zcB0, zcB1, zcB2, zcA1, zcA2;
        float shimmerAlpha, shimmerNorm;
        float envAttackCoef, envReleaseCoef;
        int zcMinPeriod;
        int zcMaxPeriod;

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
            float toneDbStart = (float)effect.ToneDb.GetValue(startPair, totalPairs, hz);
            float toneDbEnd = (float)effect.ToneDb.GetValue(endPair, totalPairs, hz);
            float mixStart = (float)(effect.Mix.GetValue(startPair, totalPairs, hz) * 0.01);
            float mixEnd = (float)(effect.Mix.GetValue(endPair, totalPairs, hz) * 0.01);

            float averageToneDb = (toneDbStart + toneDbEnd) * 0.5f;
            float roughnessFreqStart = (float)effect.RoughnessFreq.GetValue(startPair, totalPairs, hz);
            float roughnessFreqEnd = (float)effect.RoughnessFreq.GetValue(endPair, totalPairs, hz);
            float averageRoughnessFreq = (roughnessFreqStart + roughnessFreqEnd) * 0.5f;

            EnsureFilters(hz);
            UpdateShimmerCoefs(hz, averageRoughnessFreq);
            UpdateToneFilter(hz, averageToneDb);

            float invDenominator = pairs > 1 ? 1f / (pairs - 1) : 0f;
            float coef = dcBlockerCoef;
            double b0 = zcB0, b1 = zcB1, b2 = zcB2, a1 = zcA1, a2 = zcA2;
            float sAlpha = shimmerAlpha, sNorm = shimmerNorm;
            float eAttack = envAttackCoef, eRelease = envReleaseCoef;
            int minPeriod = zcMinPeriod;
            int maxPeriod = zcMaxPeriod;

            fixed (float* pBuf = &destBuffer[offset])
            {
                for (int i = 0; i < pairs; i++)
                {
                    float t = pairs > 1 ? i * invDenominator : 0f;
                    float drive = driveStart + (driveEnd - driveStart) * t;
                    float asymmetry = asymmetryStart + (asymmetryEnd - asymmetryStart) * t;
                    float roughness = roughnessStart + (roughnessEnd - roughnessStart) * t;
                    float mix = mixStart + (mixEnd - mixStart) * t;

                    int idx = i * 2;
                    float dryLeft = pBuf[idx];
                    float dryRight = pBuf[idx + 1];

                    float wetLeft = ProcessChannel(leftChannel, dryLeft, roughness, b0, b1, b2, a1, a2, sAlpha, sNorm, eAttack, eRelease, minPeriod, maxPeriod);
                    float wetRight = ProcessChannel(rightChannel, dryRight, roughness, b0, b1, b2, a1, a2, sAlpha, sNorm, eAttack, eRelease, minPeriod, maxPeriod);

                    float bias = asymmetry * AsymmetryBias;
                    float tanhBias = MathF.Tanh(bias);
                    float peakPositive = MathF.Tanh(drive + bias) - tanhBias;
                    float peakNegative = tanhBias - MathF.Tanh(-drive + bias);
                    float peakMagnitude = MathF.Max(peakPositive, peakNegative);
                    float invPeak = peakMagnitude > 1e-6f ? 1f / peakMagnitude : 1f;

                    float shapedLeft = (MathF.Tanh(wetLeft * drive + bias) - tanhBias) * invPeak;
                    float shapedRight = (MathF.Tanh(wetRight * drive + bias) - tanhBias) * invPeak;

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
        static float ProcessChannel(
            ChannelState ch, float sample, float roughness,
            double b0, double b1, double b2, double a1, double a2,
            float shimAlpha, float shimNorm,
            float envAttack, float envRelease,
            int minPeriod, int maxPeriod)
        {
            double filtered = b0 * sample + ch.ZcZ1;
            ch.ZcZ1 = b1 * sample - a1 * filtered + ch.ZcZ2;
            ch.ZcZ2 = b2 * sample - a2 * filtered;

            ch.ZcCounter++;

            if (ch.LastFiltered <= 0.0 && filtered > 0.0 && ch.ZcCounter >= minPeriod && ch.ZcCounter <= maxPeriod)
            {
                ch.SubPhaseInc = Math.PI / ch.ZcCounter;
                ch.ZcCounter = 0;
            }
            else if (ch.ZcCounter > maxPeriod)
            {
                ch.ZcCounter = 0;
            }
            ch.LastFiltered = filtered;

            ch.SubPhase += ch.SubPhaseInc;
            if (ch.SubPhase >= TwoPi) ch.SubPhase -= TwoPi;

            float subharmonic = (float)Math.Sin(ch.SubPhase);

            ch.RngState ^= ch.RngState << 13;
            ch.RngState ^= ch.RngState >> 17;
            ch.RngState ^= ch.RngState << 5;
            float noise = (float)(ch.RngState & 0xFFFFFFu) * (1f / 8388607.5f) - 1f;
            float shimmerRaw = shimAlpha * ch.ShimmerValue + (1.0f - shimAlpha) * noise;
            ch.ShimmerValue = shimmerRaw;

            float modulatedSub = subharmonic * (1f + shimmerRaw * shimNorm * ShimmerDepthScale);

            float absSample = MathF.Abs(sample);
            ch.Envelope = absSample > ch.Envelope
                ? envAttack * ch.Envelope + (1f - envAttack) * absSample
                : envRelease * ch.Envelope + (1f - envRelease) * absSample;

            return sample + modulatedSub * roughness * SubharmonicMix * ch.Envelope;
        }

        void EnsureFilters(int hz)
        {
            if (filtersInitialized) return;
            filtersInitialized = true;

            dcBlockerCoef = (float)Math.Exp(-TwoPi * DcBlockerCutoffHz / hz);

            double w0 = TwoPi * ZcLowpassCutoffHz / hz;
            double cosw0 = Math.Cos(w0);
            double sinw0 = Math.Sin(w0);
            double alpha = sinw0 / (2.0 * ZcLowpassQ);
            double inv_a0 = 1.0 / (1.0 + alpha);
            zcB0 = (1.0 - cosw0) * 0.5 * inv_a0;
            zcB1 = (1.0 - cosw0) * inv_a0;
            zcB2 = zcB0;
            zcA1 = -2.0 * cosw0 * inv_a0;
            zcA2 = (1.0 - alpha) * inv_a0;

            envAttackCoef = (float)Math.Exp(-1.0 / (EnvelopeAttackMs * hz / 1000.0));
            envReleaseCoef = (float)Math.Exp(-1.0 / (EnvelopeReleaseMs * hz / 1000.0));

            zcMinPeriod = (int)(hz / MaxFundamentalHz);
            zcMaxPeriod = (int)(hz / MinFundamentalHz);
        }

        void UpdateShimmerCoefs(int hz, float roughnessFreq)
        {
            if (!float.IsNaN(lastRoughnessFreq) && MathF.Abs(roughnessFreq - lastRoughnessFreq) <= RoughnessFreqUpdateThreshold) return;

            double sAlpha = Math.Exp(-TwoPi * roughnessFreq / hz);
            shimmerAlpha = (float)sAlpha;
            shimmerNorm = (float)Math.Sqrt(3.0 * (1.0 + sAlpha) / (1.0 - sAlpha));
            lastRoughnessFreq = roughnessFreq;
        }

        void UpdateToneFilter(int hz, float toneDb)
        {
            if (!float.IsNaN(lastToneDb) && MathF.Abs(toneDb - lastToneDb) <= ToneUpdateThreshold) return;

            toneFilter.SetHighShelf(hz, ToneShelfFrequency, ToneShelfSlope, toneDb);
            lastToneDb = toneDb;
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
            zcB0 = zcB1 = zcB2 = zcA1 = zcA2 = 0.0;
            shimmerAlpha = shimmerNorm = 0f;
            envAttackCoef = envReleaseCoef = 0f;
            zcMinPeriod = 0;
            zcMaxPeriod = 0;
            toneFilter.Reset();
            lastToneDb = float.NaN;
            lastRoughnessFreq = float.NaN;
        }

        static void ResetChannel(ChannelState ch, uint rngState)
        {
            ch.ZcZ1 = ch.ZcZ2 = 0.0;
            ch.LastFiltered = 0.0;
            ch.SubPhase = 0.0;
            ch.SubPhaseInc = 0.0;
            ch.ZcCounter = 0;
            ch.ShimmerValue = 0f;
            ch.Envelope = 0f;
            ch.RngState = rngState;
        }
    }
}
