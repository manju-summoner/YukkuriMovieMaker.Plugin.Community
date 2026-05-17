using NAudio.Dsp;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Audio;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Whisper
{
    internal class WhisperEffectProcessor : AudioEffectProcessorBase
    {
        const int FrameSize = 1024;
        const int HopSize = 256;
        const int FftOrder = 10;
        const int HalfSize = FrameSize / 2;
        const int CepstralLifterCutoff = 30;
        const float MinLogMagnitude = 1e-10f;
        const float SynthesisScale = 2f;
        const float TwoPi = (float)(Math.PI * 2.0);
        const float ShelfFrequency = 4500f;
        const float ShelfSlope = 0.9f;
        const float FilterQ = 0.707f;
        const float FilterUpdateThresholdHz = 0.5f;
        const float FilterUpdateThresholdDb = 0.05f;
        const uint RngInitialStateLeft = 0x9E3779B1u;
        const uint RngInitialStateRight = 0x6C62272Eu;

        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();
        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        readonly WhisperEffect effect;
        readonly float[] window = new float[FrameSize];
        readonly ChannelState leftChannel = new();
        readonly ChannelState rightChannel = new();
        long currentPosition;
        readonly StereoBiQuadFilter highPassFilter = new();
        readonly StereoBiQuadFilter brightnessFilter = new();
        float lastHighPassHz = -1f;
        float lastBrightnessDb = float.NaN;
        float[] dryBuffer = [];

        public WhisperEffectProcessor(WhisperEffect effect)
        {
            this.effect = effect;
            for (int i = 0; i < FrameSize; i++)
                window[i] = 0.5f - 0.5f * MathF.Cos(TwoPi * i / FrameSize);
            leftChannel.RngState = RngInitialStateLeft;
            rightChannel.RngState = RngInitialStateRight;
        }

        sealed class ChannelState
        {
            public float[] InRing = new float[FrameSize];
            public float[] OutRing = new float[FrameSize];
            public Complex[] Spectrum = new Complex[FrameSize];
            public Complex[] Cepstrum = new Complex[FrameSize];
            public int Pos;
            public int HopCounter;
            public uint RngState;
        }

        protected override unsafe int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null) return 0;

            count -= count % 2;
            if (dryBuffer.Length < count) dryBuffer = new float[count];

            int read = Input.Read(dryBuffer, 0, count);
            read -= read % 2;
            if (read <= 0) return 0;

            int hz = Hz;
            long totalPairs = Duration / 2;
            int pairs = read / 2;
            long startPair = currentPosition / 2;
            long endPair = startPair + Math.Max(0, pairs - 1);

            float breathinessStart = (float)(effect.Breathiness.GetValue(startPair, totalPairs, hz) * 0.01);
            float breathinessEnd = (float)(effect.Breathiness.GetValue(endPair, totalPairs, hz) * 0.01);
            float highPassStart = (float)effect.HighPassHz.GetValue(startPair, totalPairs, hz);
            float highPassEnd = (float)effect.HighPassHz.GetValue(endPair, totalPairs, hz);
            float brightnessStart = (float)effect.BrightnessDb.GetValue(startPair, totalPairs, hz);
            float brightnessEnd = (float)effect.BrightnessDb.GetValue(endPair, totalPairs, hz);

            float averageHighPass = (highPassStart + highPassEnd) * 0.5f;
            float averageBrightness = (brightnessStart + brightnessEnd) * 0.5f;
            EnsureFilters(hz, averageHighPass, averageBrightness);

            float invDenominator = pairs > 1 ? 1f / (pairs - 1) : 0f;

            fixed (float* pDry = dryBuffer, pDest = &destBuffer[offset])
            {
                for (int i = 0; i < pairs; i++)
                {
                    float t = pairs > 1 ? i * invDenominator : 0f;
                    float breathiness = breathinessStart + (breathinessEnd - breathinessStart) * t;

                    int idx = i * 2;
                    float dryLeft = pDry[idx];
                    float dryRight = pDry[idx + 1];

                    float wetLeft = ProcessChannelSample(leftChannel, dryLeft);
                    float wetRight = ProcessChannelSample(rightChannel, dryRight);

                    var (hpLeft, hpRight) = highPassFilter.Transform(wetLeft, wetRight);
                    var (brLeft, brRight) = brightnessFilter.Transform(hpLeft, hpRight);

                    pDest[idx] = dryLeft * (1f - breathiness) + brLeft * breathiness;
                    pDest[idx + 1] = dryRight * (1f - breathiness) + brRight * breathiness;
                }
            }

            currentPosition += read;
            return read;
        }

        float ProcessChannelSample(ChannelState ch, float sample)
        {
            ch.InRing[ch.Pos] = sample;
            float output = ch.OutRing[ch.Pos];
            ch.OutRing[ch.Pos] = 0f;

            ch.HopCounter++;
            if (ch.HopCounter >= HopSize)
            {
                ch.HopCounter = 0;
                ProcessFrame(ch);
            }

            ch.Pos++;
            if (ch.Pos >= FrameSize) ch.Pos = 0;
            return output;
        }

        unsafe void ProcessFrame(ChannelState ch)
        {
            int startIndex = ch.Pos - FrameSize + 1;
            if (startIndex < 0) startIndex += FrameSize;

            fixed (float* pIn = ch.InRing, pOut = ch.OutRing, pWindow = window)
            fixed (Complex* pSpectrum = ch.Spectrum, pCepstrum = ch.Cepstrum)
            {
                for (int k = 0; k < FrameSize; k++)
                {
                    int srcIndex = startIndex + k;
                    if (srcIndex >= FrameSize) srcIndex -= FrameSize;
                    pSpectrum[k].X = pIn[srcIndex] * pWindow[k];
                    pSpectrum[k].Y = 0f;
                }

                FastFourierTransform.FFT(true, FftOrder, ch.Spectrum);

                for (int k = 0; k < FrameSize; k++)
                {
                    float re = pSpectrum[k].X;
                    float im = pSpectrum[k].Y;
                    float magnitude = MathF.Sqrt(re * re + im * im);
                    pCepstrum[k].X = MathF.Log(magnitude + MinLogMagnitude);
                    pCepstrum[k].Y = 0f;
                }

                FastFourierTransform.FFT(false, FftOrder, ch.Cepstrum);

                for (int k = CepstralLifterCutoff; k <= FrameSize - CepstralLifterCutoff; k++)
                {
                    pCepstrum[k].X = 0f;
                    pCepstrum[k].Y = 0f;
                }

                FastFourierTransform.FFT(true, FftOrder, ch.Cepstrum);

                for (int k = 1; k < HalfSize; k++)
                {
                    float envelope = MathF.Exp(pCepstrum[k].X);
                    float phase = NextUniformPhase(ref ch.RngState);
                    float newRe = envelope * MathF.Cos(phase);
                    float newIm = envelope * MathF.Sin(phase);

                    pSpectrum[k].X = newRe;
                    pSpectrum[k].Y = newIm;
                    pSpectrum[FrameSize - k].X = newRe;
                    pSpectrum[FrameSize - k].Y = -newIm;
                }
                pSpectrum[0].X = pSpectrum[0].Y = 0f;
                pSpectrum[HalfSize].X = pSpectrum[HalfSize].Y = 0f;

                FastFourierTransform.FFT(false, FftOrder, ch.Spectrum);

                for (int k = 0; k < FrameSize; k++)
                {
                    int dstIndex = startIndex + k;
                    if (dstIndex >= FrameSize) dstIndex -= FrameSize;
                    pOut[dstIndex] += pSpectrum[k].X * pWindow[k] * SynthesisScale;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float NextUniformPhase(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return ((state & 0xFFFFFFu) * (1f / 16777215f) - 0.5f) * TwoPi;
        }

        void EnsureFilters(int hz, float highPassHz, float brightnessDb)
        {
            if (MathF.Abs(highPassHz - lastHighPassHz) > FilterUpdateThresholdHz)
            {
                highPassFilter.SetHighPass(hz, highPassHz, FilterQ);
                lastHighPassHz = highPassHz;
            }

            if (float.IsNaN(lastBrightnessDb) || MathF.Abs(brightnessDb - lastBrightnessDb) > FilterUpdateThresholdDb)
            {
                brightnessFilter.SetHighShelf(hz, ShelfFrequency, ShelfSlope, brightnessDb);
                lastBrightnessDb = brightnessDb;
            }
        }

        protected override void seek(long position)
        {
            currentPosition = position;
            Input?.Seek(position);

            foreach (var ch in new[] { leftChannel, rightChannel })
            {
                Array.Clear(ch.InRing, 0, ch.InRing.Length);
                Array.Clear(ch.OutRing, 0, ch.OutRing.Length);
                ch.Pos = 0;
                ch.HopCounter = 0;
            }
            leftChannel.RngState = RngInitialStateLeft;
            rightChannel.RngState = RngInitialStateRight;

            highPassFilter.Reset();
            brightnessFilter.Reset();
            lastHighPassHz = -1f;
            lastBrightnessDb = float.NaN;
        }
    }
}
