using System.Collections.Immutable;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.DynamicEffect
{
    internal class ConditionalDynamicsEffectProcessor(ConditionalDynamicsEffect effect, TimeSpan duration) : AudioEffectProcessorBase
    {
        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();
        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        SharedInput? sharedInput;
        BranchStream? belowBranch;
        BranchStream? aboveBranch;

        EffectChain? belowChain;
        EffectChain? aboveChain;

        float[] rmsWindowBuffer = [];
        int rmsWindowPos = 0;
        float rmsRunningSum = 0f;
        float rmsReciprocal = 1f;

        float sidechainEnvelope = 0f;
        float crossfadeGain = 0f;

        float[] rawBuffer = [];
        float[] belowBuffer = [];
        float[] aboveBuffer = [];

        bool isInitialized = false;

        void EnsureInitialized()
        {
            if (isInitialized) return;

            int hz = Hz;

            sharedInput = new SharedInput(Input!);
            belowBranch = new BranchStream(sharedInput, Position);
            aboveBranch = new BranchStream(sharedInput, Position);

            belowChain = EffectChain.Build(effect.BelowEffects, belowBranch, duration);
            aboveChain = EffectChain.Build(effect.AboveEffects, aboveBranch, duration);

            if (belowChain is not null) disposer.Collect(belowChain);
            if (aboveChain is not null) disposer.Collect(aboveChain);

            int windowSamples = Math.Max(1, (int)(effect.RmsWindowMs * 0.001 * hz));
            rmsWindowBuffer = new float[windowSamples];
            rmsReciprocal = 1f / windowSamples;

            isInitialized = true;
        }

        void EnsureBuffers(int count)
        {
            if (rawBuffer.Length < count) rawBuffer = new float[count];
            if (belowBuffer.Length < count) belowBuffer = new float[count];
            if (aboveBuffer.Length < count) aboveBuffer = new float[count];
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null) return 0;

            EnsureInitialized();

            count -= count % 2;
            EnsureBuffers(count);

            int readCount = sharedInput!.Read(Position, rawBuffer, 0, count);
            readCount -= readCount % 2;

            if (readCount < count)
                Array.Clear(rawBuffer, Math.Max(0, readCount), count - Math.Max(0, readCount));

            int belowRead = 0;
            if (belowChain is not null)
            {
                belowRead = belowChain.Read(belowBuffer, count);
                belowRead -= belowRead % 2;
            }
            else if (readCount > 0)
            {
                Array.Copy(rawBuffer, 0, belowBuffer, 0, readCount);
                belowRead = readCount;
                belowBranch!.Seek(belowBranch.Position + readCount);
            }

            int aboveRead = 0;
            if (aboveChain is not null)
            {
                aboveRead = aboveChain.Read(aboveBuffer, count);
                aboveRead -= aboveRead % 2;
            }
            else if (readCount > 0)
            {
                Array.Copy(rawBuffer, 0, aboveBuffer, 0, readCount);
                aboveRead = readCount;
                aboveBranch!.Seek(aboveBranch.Position + readCount);
            }

            int processCount = Math.Max(readCount, Math.Max(belowRead, aboveRead));
            if (processCount <= 0) return 0;

            if (belowRead < processCount) Array.Clear(belowBuffer, belowRead, processCount - belowRead);
            if (aboveRead < processCount) Array.Clear(aboveBuffer, aboveRead, processCount - aboveRead);

            int hz = Hz;
            float attackCoeff = ComputeCoeff(effect.AttackMs, hz);
            float releaseCoeff = ComputeCoeff(effect.ReleaseMs, hz);
            float peakHoldReleaseCoeff = ComputeCoeff(10.0, hz);

            long totalPairs = Duration / 2;
            long startPos = Position / 2;
            long endPos = (Position + processCount - 1) / 2;
            
            float startDb = (float)effect.ThresholdDb.GetValue(startPos, totalPairs, hz);
            float endDb = (float)effect.ThresholdDb.GetValue(endPos, totalPairs, hz);

            float startLinear = DbToLinear(startDb);
            float endLinear = DbToLinear(endDb);
            int stepCount = processCount / 2;
            float linearStep = stepCount > 1 ? (endLinear - startLinear) / (stepCount - 1) : 0f;
            float currentThresholdLinear = startLinear;

            bool isPeak = effect.DetectionMode == DetectionMode.Peak;

            for (int i = 0; i < processCount; i += 2)
            {
                float scLevel;
                if (isPeak)
                {
                    float peak = Math.Max(Math.Abs(rawBuffer[i]), Math.Abs(rawBuffer[i + 1]));
                    if (peak > sidechainEnvelope)
                        sidechainEnvelope = peak;
                    else
                        sidechainEnvelope = peakHoldReleaseCoeff * sidechainEnvelope + (1f - peakHoldReleaseCoeff) * peak;
                    scLevel = sidechainEnvelope;
                }
                else
                {
                    float mono = (rawBuffer[i] + rawBuffer[i + 1]) * 0.5f;
                    float sq = mono * mono;

                    rmsRunningSum -= rmsWindowBuffer[rmsWindowPos];
                    rmsWindowBuffer[rmsWindowPos] = sq;
                    rmsRunningSum += sq;
                    
                    rmsWindowPos++;
                    if (rmsWindowPos >= rmsWindowBuffer.Length)
                        rmsWindowPos = 0;

                    if (rmsWindowPos == 0)
                    {
                        float recomputed = 0f;
                        for (int j = 0; j < rmsWindowBuffer.Length; j++)
                            recomputed += rmsWindowBuffer[j];
                        rmsRunningSum = recomputed;
                    }

                    scLevel = (float)Math.Sqrt(Math.Max(0f, rmsRunningSum) * rmsReciprocal);
                }

                float targetGain = scLevel >= currentThresholdLinear ? 1f : 0f;
                currentThresholdLinear += linearStep;

                float coeff = targetGain > crossfadeGain ? attackCoeff : releaseCoeff;
                crossfadeGain = coeff * crossfadeGain + (1f - coeff) * targetGain;

                float aboveFactor = crossfadeGain;
                float belowFactor = 1f - aboveFactor;

                destBuffer[offset + i] = belowFactor * belowBuffer[i] + aboveFactor * aboveBuffer[i];
                destBuffer[offset + i + 1] = belowFactor * belowBuffer[i + 1] + aboveFactor * aboveBuffer[i + 1];
            }

            long nextPos = Position + processCount;
            long minPos = Math.Min(nextPos, Math.Min(belowBranch!.Position, aboveBranch!.Position));
            sharedInput.Trim(minPos, nextPos, hz);

            return processCount;
        }

        protected override void seek(long position)
        {
            if (!isInitialized)
            {
                Input?.Seek(position);
                return;
            }

            sharedInput?.Seek(position);

            if (belowChain is not null) belowChain.Seek(position);
            else belowBranch?.Seek(position);

            if (aboveChain is not null) aboveChain.Seek(position);
            else aboveBranch?.Seek(position);

            sidechainEnvelope = 0f;
            crossfadeGain = 0f;
            rmsWindowPos = 0;
            rmsRunningSum = 0f;
            if (rmsWindowBuffer.Length > 0)
                Array.Clear(rmsWindowBuffer, 0, rmsWindowBuffer.Length);
        }

        static float ComputeCoeff(double timeMs, int hz)
        {
            return (float)Math.Exp(-1.0 / (Math.Max(0.1, timeMs) * 0.001 * hz));
        }

        static float DbToLinear(double db)
        {
            return db <= -60.0 ? 0f : (float)Math.Pow(10.0, db / 20.0);
        }

        sealed class SharedInput(IAudioStream source)
        {
            public int Hz => source.Hz;
            public long Duration => source.Duration;

            long basePosition = source.Position;
            float[] buffer = [];
            int length = 0;

            public int Read(long position, float[] dest, int offset, int count)
            {
                if (position < basePosition || position > basePosition + length)
                {
                    source.Seek(position);
                    basePosition = position;
                    length = 0;
                }

                int available = (int)(basePosition + length - position);
                if (available < count)
                {
                    int toRead = count - available;
                    EnsureCapacity(length + toRead);
                    int r = source.Read(buffer, length, toRead);
                    length += r;
                    available = (int)(basePosition + length - position);
                }

                int toCopy = Math.Min(count, available);
                if (toCopy <= 0) return 0;

                int bufferOffset = (int)(position - basePosition);
                Array.Copy(buffer, bufferOffset, dest, offset, toCopy);
                return toCopy;
            }

            void EnsureCapacity(int cap)
            {
                if (buffer.Length >= cap) return;
                int next = Math.Max(cap, buffer.Length * 2);
                if (next < 4096) next = 4096;
                Array.Resize(ref buffer, next);
            }

            public void Trim(long minPosition, long currentPosition, int hz)
            {
                long maxLag = hz * 20L;
                long safeMin = Math.Max(minPosition, currentPosition - maxLag);

                if (safeMin <= basePosition) return;
                long advance = safeMin - basePosition;
                if (advance >= length)
                {
                    basePosition = safeMin;
                    length = 0;
                }
                else
                {
                    int advInt = (int)advance;
                    Array.Copy(buffer, advInt, buffer, 0, length - advInt);
                    basePosition = safeMin;
                    length -= advInt;
                }
            }

            public void Seek(long position)
            {
                source.Seek(position);
                basePosition = position;
                length = 0;
            }
        }

        sealed class BranchStream(SharedInput source, long position) : IAudioStream
        {
            public int Hz => source.Hz;
            public long Duration => source.Duration;
            public long Position { get; private set; } = position;

            public int Read(float[] destBuffer, int offset, int count)
            {
                int r = source.Read(Position, destBuffer, offset, count);
                Position += r;
                return r;
            }

            public void Seek(long position) => Position = position;
            public void Seek(TimeSpan time) => Seek((long)(time.TotalSeconds * Hz * 2));
            public void Dispose() { }
        }

        sealed class EffectChain : IDisposable
        {
            readonly IAudioEffectProcessor head;
            readonly List<IAudioEffectProcessor> allProcessors;

            EffectChain(IAudioEffectProcessor head, List<IAudioEffectProcessor> allProcessors)
            {
                this.head = head;
                this.allProcessors = allProcessors;
            }

            public static EffectChain? Build(ImmutableList<IAudioEffect> effects, IAudioStream source, TimeSpan timeSpan)
            {
                if (effects.IsEmpty) return null;

                var all = new List<IAudioEffectProcessor>();
                IAudioStream current = source;

                foreach (var eff in effects)
                {
                    var proc = eff.CreateAudioEffect(timeSpan);
                    proc.Input = current;
                    current = proc;
                    all.Add(proc);
                }

                return new EffectChain((IAudioEffectProcessor)current, all);
            }

            public int Read(float[] destBuffer, int count) => head.Read(destBuffer, 0, count);

            public void Seek(long position) => head.Seek(position);

            public void Dispose()
            {
                foreach (var proc in allProcessors)
                    proc.Dispose();
            }
        }
    }
}
