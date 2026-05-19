using NAudio.Dsp;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Falsetto
{
    internal class FalsettoEffectProcessor : AudioEffectProcessorBase
    {
        const int FrameSize = 2048;
        const int HopSize = 512;
        const int FftOrder = 11;
        const int HalfSize = FrameSize / 2;
        const int MaxPeaks = 256;
        const int TrueEnvelopeMaxIterations = 6;
        const float TrueEnvelopeConvergenceLog = 0.23026f;
        const float TransientFluxThreshold = 0.55f;
        const float OlaNormalization = 2f / 3f;
        const float TwoPi = (float)(Math.PI * 2.0);
        const float Pi = (float)Math.PI;
        const float MinMagnitude = 1e-6f;
        const float MinLogMagnitude = 1e-9f;
        const float EnvelopeTimeConstantSeconds = 0.005f;
        const float LifterCutoffSeconds = 0.0014f;
        const float BreathHighpassCutoffHz = 200f;
        const uint RngInitialState = 0xC2B2AE3Du;

        const int TailFlushSamples = FrameSize - HopSize;

        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();
        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        readonly FalsettoEffect effect;
        readonly float[] window = new float[FrameSize];
        readonly ChannelState[] channels = [new ChannelState(), new ChannelState()];
        long currentPosition;
        uint rngState = RngInitialState;
        float envFollowerLeft, envFollowerRight;
        float breathHpStateLeft, breathHpStateRight;
        float breathHpPrevInputLeft, breathHpPrevInputRight;
        float[] dryBuffer = [];
        int cachedLifterCount = -1;
        int cachedSampleRate;
        int tailRemaining;
        readonly float[] lifterWindow = new float[HalfSize + 1];

        public FalsettoEffectProcessor(FalsettoEffect effect)
        {
            this.effect = effect;
            for (int i = 0; i < FrameSize; i++)
                window[i] = 0.5f - 0.5f * MathF.Cos(TwoPi * i / FrameSize);
        }

        sealed class ChannelState
        {
            public float[] InRing = new float[FrameSize];
            public float[] OutRing = new float[FrameSize];
            public Complex[] Fft = new Complex[FrameSize];
            public Complex[] CepstrumWork = new Complex[FrameSize];
            public float[] AnalysisMagnitude = new float[HalfSize + 1];
            public float[] AnalysisPhase = new float[HalfSize + 1];
            public float[] AnalysisLogMagnitude = new float[HalfSize + 1];
            public float[] PrevAnalysisMagnitude = new float[HalfSize + 1];
            public float[] LastAnalysisPhase = new float[HalfSize + 1];
            public float[] SynthesisPhase = new float[HalfSize + 1];
            public float[] TrueEnvelopeLog = new float[HalfSize + 1];
            public float[] SourceEnvelope = new float[HalfSize + 1];
            public float[] TargetEnvelope = new float[HalfSize + 1];
            public float[] OutMagnitude = new float[HalfSize + 1];
            public float[] OutPhase = new float[HalfSize + 1];
            public int[] PeakBins = new int[MaxPeaks];
            public float[] PeakTrueBins = new float[MaxPeaks];
            public float[] PeakSynthesisPhase = new float[MaxPeaks];
            public int[] PrevPeakBins = new int[MaxPeaks];
            public float[] PrevPeakTrueBins = new float[MaxPeaks];
            public float[] PrevPeakSynthesisPhase = new float[MaxPeaks];
            public int PeakCount;
            public int PrevPeakCount;
            public bool HasPreviousFrame;
            public int Pos;
            public int HopCounter;
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null) return 0;

            count -= count % 2;
            if (count <= 0) return 0;
            if (dryBuffer.Length < count) dryBuffer = new float[count];

            int read = Input.Read(dryBuffer, 0, count);
            read -= read % 2;
            if (read < 0) read = 0;

            int producedSamples;
            if (read >= count)
            {
                producedSamples = count;
                tailRemaining = TailFlushSamples;
            }
            else
            {
                int remainingPairs = tailRemaining;
                if (remainingPairs <= 0)
                {
                    if (read <= 0) return 0;
                    producedSamples = read;
                }
                else
                {
                    int availableTailSamples = remainingPairs * 2;
                    int padSamples = count - read;
                    if (padSamples > availableTailSamples) padSamples = availableTailSamples;
                    Array.Clear(dryBuffer, read, padSamples);
                    producedSamples = read + padSamples;
                    tailRemaining = remainingPairs - padSamples / 2;
                }
            }

            int hz = Hz;
            long totalPairs = Duration / 2;
            int pairs = producedSamples / 2;
            long startPair = currentPosition / 2;
            long endPair = startPair + Math.Max(0, pairs - 1);

            EnsureLifterWindow(hz);

            float pitchStart = (float)effect.PitchSemitones.GetValue(startPair, totalPairs, hz);
            float pitchEnd = (float)effect.PitchSemitones.GetValue(endPair, totalPairs, hz);
            float formantStart = (float)effect.FormantSemitones.GetValue(startPair, totalPairs, hz);
            float formantEnd = (float)effect.FormantSemitones.GetValue(endPair, totalPairs, hz);
            float breathStart = (float)(effect.Breath.GetValue(startPair, totalPairs, hz) * 0.01);
            float breathEnd = (float)(effect.Breath.GetValue(endPair, totalPairs, hz) * 0.01);
            float mixStart = (float)(effect.Mix.GetValue(startPair, totalPairs, hz) * 0.01);
            float mixEnd = (float)(effect.Mix.GetValue(endPair, totalPairs, hz) * 0.01);

            float invDenominator = pairs > 1 ? 1f / (pairs - 1) : 0f;
            float envelopeCoefficient = 1f - MathF.Exp(-1f / (hz * EnvelopeTimeConstantSeconds));
            float breathHpAlpha = MathF.Exp(-TwoPi * BreathHighpassCutoffHz / hz);

            for (int i = 0; i < pairs; i++)
            {
                float t = pairs > 1 ? i * invDenominator : 0f;
                float pitchSemitones = pitchStart + (pitchEnd - pitchStart) * t;
                float formantSemitones = formantStart + (formantEnd - formantStart) * t;
                float breath = breathStart + (breathEnd - breathStart) * t;
                float mix = mixStart + (mixEnd - mixStart) * t;

                float pitchRatio = MathF.Pow(2f, pitchSemitones / 12f);
                float formantRatio = MathF.Pow(2f, formantSemitones / 12f);

                int idx = offset + i * 2;
                int dryIdx = i * 2;
                float dryLeft = dryBuffer[dryIdx];
                float dryRight = dryBuffer[dryIdx + 1];

                float absLeft = MathF.Abs(dryLeft);
                float absRight = MathF.Abs(dryRight);
                envFollowerLeft += envelopeCoefficient * (absLeft - envFollowerLeft);
                envFollowerRight += envelopeCoefficient * (absRight - envFollowerRight);

                float wetLeft = ProcessChannelSample(channels[0], dryLeft, pitchRatio, formantRatio);
                float wetRight = ProcessChannelSample(channels[1], dryRight, pitchRatio, formantRatio);

                float rawNoiseLeft = NextNoise();
                float rawNoiseRight = NextNoise();
                float hpNoiseLeft = breathHpAlpha * (breathHpStateLeft + rawNoiseLeft - breathHpPrevInputLeft);
                float hpNoiseRight = breathHpAlpha * (breathHpStateRight + rawNoiseRight - breathHpPrevInputRight);
                breathHpStateLeft = hpNoiseLeft;
                breathHpStateRight = hpNoiseRight;
                breathHpPrevInputLeft = rawNoiseLeft;
                breathHpPrevInputRight = rawNoiseRight;

                float noiseLeft = hpNoiseLeft * envFollowerLeft * breath;
                float noiseRight = hpNoiseRight * envFollowerRight * breath;

                wetLeft += noiseLeft;
                wetRight += noiseRight;

                destBuffer[idx] = dryLeft * (1f - mix) + wetLeft * mix;
                destBuffer[idx + 1] = dryRight * (1f - mix) + wetRight * mix;
            }

            currentPosition += producedSamples;
            return producedSamples;
        }

        void EnsureLifterWindow(int hz)
        {
            if (hz == cachedSampleRate && cachedLifterCount > 0) return;
            cachedSampleRate = hz;
            int lifterBins = (int)MathF.Round(LifterCutoffSeconds * hz);
            if (lifterBins < 4) lifterBins = 4;
            if (lifterBins > HalfSize) lifterBins = HalfSize;
            cachedLifterCount = lifterBins;
            for (int n = 0; n <= HalfSize; n++)
            {
                if (n < lifterBins)
                {
                    float x = (float)n / lifterBins;
                    lifterWindow[n] = 0.5f + 0.5f * MathF.Cos(Pi * x);
                }
                else
                {
                    lifterWindow[n] = 0f;
                }
            }
        }

        float ProcessChannelSample(ChannelState ch, float sample, float pitchRatio, float formantRatio)
        {
            ch.InRing[ch.Pos] = sample;
            ch.HopCounter++;
            if (ch.HopCounter >= HopSize)
            {
                ch.HopCounter = 0;
                ProcessFrame(ch, pitchRatio, formantRatio);
            }
            int nextPos = ch.Pos + 1;
            if (nextPos >= FrameSize) nextPos = 0;
            float output = ch.OutRing[nextPos];
            ch.OutRing[nextPos] = 0f;
            ch.Pos = nextPos;
            return output;
        }

        void ProcessFrame(ChannelState ch, float pitchRatio, float formantRatio)
        {
            int startIndex = ch.Pos + 1;
            if (startIndex >= FrameSize) startIndex = 0;

            for (int k = 0; k < FrameSize; k++)
            {
                int srcIndex = startIndex + k;
                if (srcIndex >= FrameSize) srcIndex -= FrameSize;
                ch.Fft[k].X = ch.InRing[srcIndex] * window[k];
                ch.Fft[k].Y = 0f;
            }

            FastFourierTransform.FFT(true, FftOrder, ch.Fft);

            for (int k = 0; k <= HalfSize; k++)
            {
                float re = ch.Fft[k].X;
                float im = ch.Fft[k].Y;
                float magnitude = MathF.Sqrt(re * re + im * im);
                ch.AnalysisMagnitude[k] = magnitude;
                ch.AnalysisPhase[k] = magnitude > MinMagnitude ? MathF.Atan2(im, re) : 0f;
                ch.AnalysisLogMagnitude[k] = MathF.Log(magnitude + MinLogMagnitude);
            }

            bool transient = ch.HasPreviousFrame && DetectTransient(ch.AnalysisMagnitude, ch.PrevAnalysisMagnitude);

            for (int k = 0; k <= HalfSize; k++)
                ch.PrevAnalysisMagnitude[k] = ch.AnalysisMagnitude[k];

            EstimateTrueEnvelope(ch.AnalysisLogMagnitude, ch.TrueEnvelopeLog, ch.CepstrumWork, lifterWindow);

            for (int k = 0; k <= HalfSize; k++)
                ch.SourceEnvelope[k] = MathF.Exp(ch.TrueEnvelopeLog[k]);

            float invFormantRatio = 1f / formantRatio;
            for (int k = 0; k <= HalfSize; k++)
            {
                float srcK = k * invFormantRatio;
                int srcK0 = (int)MathF.Floor(srcK);
                float frac = srcK - srcK0;
                if (srcK0 < 0) { srcK0 = 0; frac = 0f; }
                else if (srcK0 >= HalfSize) { srcK0 = HalfSize; frac = 0f; }
                int srcK1 = srcK0 + 1;
                if (srcK1 > HalfSize) srcK1 = HalfSize;
                ch.TargetEnvelope[k] = ch.SourceEnvelope[srcK0] * (1f - frac) + ch.SourceEnvelope[srcK1] * frac;
            }

            int peakCount = DetectPeaks(ch.AnalysisMagnitude, ch.PeakBins, ch.PeakTrueBins, ch.AnalysisLogMagnitude);
            ch.PeakCount = peakCount;

            Array.Clear(ch.OutMagnitude, 0, HalfSize + 1);
            Array.Clear(ch.OutPhase, 0, HalfSize + 1);

            float twoPiHopOverN = TwoPi * HopSize / FrameSize;

            if (transient || !ch.HasPreviousFrame)
            {
                for (int k = 0; k <= HalfSize; k++)
                {
                    ch.LastAnalysisPhase[k] = ch.AnalysisPhase[k];
                    ch.SynthesisPhase[k] = ch.AnalysisPhase[k];
                    ch.OutMagnitude[k] = ch.AnalysisMagnitude[k];
                    ch.OutPhase[k] = ch.AnalysisPhase[k];
                }
            }
            else if (peakCount == 0)
            {
                ShiftPhaseVocoder(ch, pitchRatio, twoPiHopOverN);
            }
            else
            {
                ShiftPeakRegions(ch, peakCount, pitchRatio, twoPiHopOverN);
            }

            for (int k = 0; k <= HalfSize; k++)
                ch.LastAnalysisPhase[k] = ch.AnalysisPhase[k];

            for (int p = 0; p < peakCount; p++)
            {
                ch.PrevPeakBins[p] = ch.PeakBins[p];
                ch.PrevPeakTrueBins[p] = ch.PeakTrueBins[p];
                ch.PrevPeakSynthesisPhase[p] = ch.PeakSynthesisPhase[p];
            }
            ch.PrevPeakCount = peakCount;
            ch.HasPreviousFrame = true;

            for (int k = 0; k <= HalfSize; k++)
            {
                float magnitude = ch.OutMagnitude[k];
                if (magnitude < MinMagnitude || k == 0 || k == HalfSize)
                {
                    ch.Fft[k].X = 0f;
                    ch.Fft[k].Y = 0f;
                    continue;
                }
                float phase = ch.OutPhase[k];
                ch.Fft[k].X = magnitude * MathF.Cos(phase);
                ch.Fft[k].Y = magnitude * MathF.Sin(phase);
            }

            for (int k = 1; k < HalfSize; k++)
            {
                ch.Fft[FrameSize - k].X = ch.Fft[k].X;
                ch.Fft[FrameSize - k].Y = -ch.Fft[k].Y;
            }

            FastFourierTransform.FFT(false, FftOrder, ch.Fft);

            for (int k = 0; k < FrameSize; k++)
            {
                int dstIndex = startIndex + k;
                if (dstIndex >= FrameSize) dstIndex -= FrameSize;
                ch.OutRing[dstIndex] += ch.Fft[k].X * window[k] * OlaNormalization;
            }
        }

        static bool DetectTransient(float[] mag, float[] prevMag)
        {
            float flux = 0f;
            float prevEnergy = 0f;
            for (int k = 0; k <= HalfSize; k++)
            {
                float diff = mag[k] - prevMag[k];
                if (diff > 0f) flux += diff;
                prevEnergy += prevMag[k];
            }
            float normalized = flux / (prevEnergy + MinMagnitude);
            return normalized > TransientFluxThreshold;
        }

        static void EstimateTrueEnvelope(float[] logMag, float[] envOut, Complex[] work, float[] lifter)
        {
            for (int k = 0; k <= HalfSize; k++)
                envOut[k] = logMag[k];

            for (int iter = 0; iter < TrueEnvelopeMaxIterations; iter++)
            {
                CepstralSmooth(envOut, work, lifter);

                float maxResidual = 0f;
                for (int k = 0; k <= HalfSize; k++)
                {
                    float a = logMag[k];
                    float v = envOut[k];
                    if (a > v)
                    {
                        float diff = a - v;
                        if (diff > maxResidual) maxResidual = diff;
                        envOut[k] = a;
                    }
                }
                if (maxResidual < TrueEnvelopeConvergenceLog) break;
            }

            CepstralSmooth(envOut, work, lifter);
        }

        static void CepstralSmooth(float[] logMag, Complex[] work, float[] lifter)
        {
            for (int k = 0; k < FrameSize; k++)
            {
                int mirrorK = k <= HalfSize ? k : FrameSize - k;
                work[k].X = logMag[mirrorK];
                work[k].Y = 0f;
            }

            FastFourierTransform.FFT(true, FftOrder, work);

            for (int k = 0; k < FrameSize; k++)
            {
                int mirrorK = k <= HalfSize ? k : FrameSize - k;
                float w = lifter[mirrorK];
                work[k].X *= w;
                work[k].Y *= w;
            }

            FastFourierTransform.FFT(false, FftOrder, work);

            for (int k = 0; k <= HalfSize; k++)
                logMag[k] = work[k].X;
        }

        static int DetectPeaks(float[] mag, int[] peakBins, float[] peakTrueBins, float[] logMag)
        {
            int count = 0;
            for (int k = 2; k <= HalfSize - 2 && count < MaxPeaks; k++)
            {
                float m = mag[k];
                if (m <= MinMagnitude) continue;
                if (m <= mag[k - 1] || m <= mag[k - 2]) continue;
                if (m <= mag[k + 1] || m <= mag[k + 2]) continue;

                peakBins[count] = k;
                float a = logMag[k - 1];
                float b = logMag[k];
                float c = logMag[k + 1];
                float denom = a - 2f * b + c;
                float delta = MathF.Abs(denom) > 1e-12f ? 0.5f * (a - c) / denom : 0f;
                if (delta > 0.5f) delta = 0.5f;
                else if (delta < -0.5f) delta = -0.5f;
                peakTrueBins[count] = k + delta;
                count++;
            }
            return count;
        }

        static void ShiftPeakRegions(ChannelState ch, int peakCount, float pitchRatio, float twoPiHopOverN)
        {
            int prevPeakCount = ch.PrevPeakCount;

            for (int p = 0; p < peakCount; p++)
            {
                int kp = ch.PeakBins[p];
                float truebp = ch.PeakTrueBins[p];
                int regionLo = p == 0 ? 0 : (ch.PeakBins[p - 1] + kp + 1) / 2;
                int regionHi = p == peakCount - 1 ? HalfSize : (kp + ch.PeakBins[p + 1]) / 2;

                float newTrueBin = truebp * pitchRatio;
                if (newTrueBin < 0f || newTrueBin > HalfSize)
                {
                    ch.PeakSynthesisPhase[p] = 0f;
                    continue;
                }
                int newKp = (int)MathF.Round(newTrueBin);
                int delta = newKp - kp;

                int matchIdx = FindNearestPrevPeak(ch.PrevPeakBins, prevPeakCount, kp);
                float prevSynthPhase;
                float prevTrueBin;
                if (matchIdx >= 0)
                {
                    prevSynthPhase = ch.PrevPeakSynthesisPhase[matchIdx];
                    prevTrueBin = ch.PrevPeakTrueBins[matchIdx];
                }
                else
                {
                    prevSynthPhase = ch.AnalysisPhase[kp];
                    prevTrueBin = truebp;
                }
                float newPrevTrueBin = prevTrueBin * pitchRatio;

                float synthPhaseIncrement = twoPiHopOverN * 0.5f * (newPrevTrueBin + newTrueBin);
                float newPeakSynthPhase = WrapPhase(prevSynthPhase + synthPhaseIncrement);
                ch.PeakSynthesisPhase[p] = newPeakSynthPhase;

                float phaseRotation = newPeakSynthPhase - ch.AnalysisPhase[kp];

                for (int k = regionLo; k <= regionHi; k++)
                {
                    int newK = k + delta;
                    if (newK < 0 || newK > HalfSize) continue;

                    float env = ch.SourceEnvelope[k];
                    float excitation = env > MinMagnitude ? ch.AnalysisMagnitude[k] / env : 0f;
                    if (excitation <= 0f) continue;

                    float newMag = excitation * ch.TargetEnvelope[newK];
                    if (newMag <= ch.OutMagnitude[newK]) continue;

                    float rotated = WrapPhase(ch.AnalysisPhase[k] + phaseRotation);
                    ch.OutMagnitude[newK] = newMag;
                    ch.OutPhase[newK] = rotated;
                }
            }
        }

        static void ShiftPhaseVocoder(ChannelState ch, float pitchRatio, float twoPiHopOverN)
        {
            for (int k = 0; k <= HalfSize; k++)
            {
                float env = ch.SourceEnvelope[k];
                float excitation = env > MinMagnitude ? ch.AnalysisMagnitude[k] / env : 0f;
                if (excitation <= 0f) continue;

                float deltaPhase = ch.AnalysisPhase[k] - ch.LastAnalysisPhase[k] - k * twoPiHopOverN;
                deltaPhase = WrapPhase(deltaPhase);
                float trueBin = k + deltaPhase / twoPiHopOverN;
                float newTrueBin = trueBin * pitchRatio;
                float synthPhaseIncrement = twoPiHopOverN * newTrueBin;
                float instPhase = WrapPhase(ch.SynthesisPhase[k] + synthPhaseIncrement);
                ch.SynthesisPhase[k] = instPhase;

                float newKf = k * pitchRatio;
                if (newKf < 0f || newKf > HalfSize) continue;

                int newK0 = (int)MathF.Floor(newKf);
                float frac = newKf - newK0;
                int newK1 = newK0 + 1;
                if (newK1 > HalfSize) { newK1 = HalfSize; frac = 0f; }

                float w0 = 1f - frac;
                float w1 = frac;

                float mag0 = excitation * ch.TargetEnvelope[newK0] * w0;
                float mag1 = excitation * ch.TargetEnvelope[newK1] * w1;

                if (mag0 > ch.OutMagnitude[newK0])
                {
                    ch.OutMagnitude[newK0] = mag0;
                    ch.OutPhase[newK0] = instPhase;
                }
                if (newK1 != newK0 && mag1 > ch.OutMagnitude[newK1])
                {
                    ch.OutMagnitude[newK1] = mag1;
                    ch.OutPhase[newK1] = instPhase;
                }
            }
        }

        static int FindNearestPrevPeak(int[] prevPeakBins, int prevCount, int currentBin)
        {
            if (prevCount <= 0) return -1;
            int bestIdx = -1;
            int bestDist = int.MaxValue;
            for (int i = 0; i < prevCount; i++)
            {
                int dist = prevPeakBins[i] - currentBin;
                if (dist < 0) dist = -dist;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
            return bestDist <= 4 ? bestIdx : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float WrapPhase(float phase)
        {
            return phase - TwoPi * MathF.Round(phase / TwoPi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float NextNoise()
        {
            rngState ^= rngState << 13;
            rngState ^= rngState >> 17;
            rngState ^= rngState << 5;
            return ((rngState >> 8) * (1f / 16777216f) - 0.5f) * 2f;
        }

        protected override void seek(long position)
        {
            currentPosition = position;
            Input?.Seek(position);
            foreach (var ch in channels)
            {
                Array.Clear(ch.InRing, 0, ch.InRing.Length);
                Array.Clear(ch.OutRing, 0, ch.OutRing.Length);
                Array.Clear(ch.AnalysisMagnitude, 0, ch.AnalysisMagnitude.Length);
                Array.Clear(ch.AnalysisPhase, 0, ch.AnalysisPhase.Length);
                Array.Clear(ch.AnalysisLogMagnitude, 0, ch.AnalysisLogMagnitude.Length);
                Array.Clear(ch.PrevAnalysisMagnitude, 0, ch.PrevAnalysisMagnitude.Length);
                Array.Clear(ch.LastAnalysisPhase, 0, ch.LastAnalysisPhase.Length);
                Array.Clear(ch.SynthesisPhase, 0, ch.SynthesisPhase.Length);
                Array.Clear(ch.TrueEnvelopeLog, 0, ch.TrueEnvelopeLog.Length);
                Array.Clear(ch.SourceEnvelope, 0, ch.SourceEnvelope.Length);
                Array.Clear(ch.TargetEnvelope, 0, ch.TargetEnvelope.Length);
                Array.Clear(ch.OutMagnitude, 0, ch.OutMagnitude.Length);
                Array.Clear(ch.OutPhase, 0, ch.OutPhase.Length);
                Array.Clear(ch.PeakBins, 0, ch.PeakBins.Length);
                Array.Clear(ch.PeakTrueBins, 0, ch.PeakTrueBins.Length);
                Array.Clear(ch.PeakSynthesisPhase, 0, ch.PeakSynthesisPhase.Length);
                Array.Clear(ch.PrevPeakBins, 0, ch.PrevPeakBins.Length);
                Array.Clear(ch.PrevPeakTrueBins, 0, ch.PrevPeakTrueBins.Length);
                Array.Clear(ch.PrevPeakSynthesisPhase, 0, ch.PrevPeakSynthesisPhase.Length);
                ch.PeakCount = 0;
                ch.PrevPeakCount = 0;
                ch.HasPreviousFrame = false;
                ch.Pos = 0;
                ch.HopCounter = 0;
            }
            envFollowerLeft = 0f;
            envFollowerRight = 0f;
            breathHpStateLeft = 0f;
            breathHpStateRight = 0f;
            breathHpPrevInputLeft = 0f;
            breathHpPrevInputRight = 0f;
            rngState = RngInitialState;
            tailRemaining = 0;
        }
    }
}
