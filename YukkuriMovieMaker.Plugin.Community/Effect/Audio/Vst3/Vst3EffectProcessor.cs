using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal class Vst3EffectProcessor(Vst3Effect effect) : AudioEffectProcessorBase
    {
        const int BlockFrames = Vst3Instance.BlockFrames;

        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();

        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        Vst3InstanceLease? lease;
        bool isLeaseCreated;
        float[] dryBuffer = [];
        float[] primeBuffer = [];
        float[] delayLine = [];
        int delayIndex;
        long tailRemaining;
        long position;
        long inputFramePosition;

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null)
                return 0;
            count -= count % 2;

            EnsureLease();
            var plugin = lease?.Instance;
            var read = Input.Read(destBuffer, offset, count);
            read -= read % 2;

            if (plugin is not null)
            {
                if (read < count && tailRemaining > 0)
                {
                    var padding = (int)Math.Min(count - read, tailRemaining);
                    padding -= padding % 2;
                    Array.Clear(destBuffer, offset + read, padding);
                    tailRemaining -= padding;
                    read += padding;
                }
                if (read > 0)
                {
                    if (dryBuffer.Length < read)
                        dryBuffer = new float[read];
                    if (delayLine.Length > 0)
                    {
                        for (var i = 0; i < read; i++)
                        {
                            dryBuffer[i] = delayLine[delayIndex];
                            delayLine[delayIndex] = destBuffer[offset + i];
                            delayIndex = (delayIndex + 1) % delayLine.Length;
                        }
                    }
                    else
                    {
                        Array.Copy(destBuffer, offset, dryBuffer, 0, read);
                    }

                    plugin.Process(destBuffer, offset, read / 2, inputFramePosition, CreateTransport());
                    inputFramePosition += read / 2;

                    var total = Duration / 2;
                    for (var i = 0; i < read; i += BlockFrames * 2)
                    {
                        var mix = (float)(effect.Mix.GetValue((position + i) / 2, total, Hz) / 100);
                        var blockLength = Math.Min(BlockFrames * 2, read - i);
                        for (var j = 0; j < blockLength; j++)
                        {
                            var index = offset + i + j;
                            destBuffer[index] = dryBuffer[i + j] * (1 - mix) + destBuffer[index] * mix;
                        }
                    }
                }
            }

            position += read;
            return read;
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            this.position = position;
            inputFramePosition = position / 2;
            lease?.Instance.Reset();
            Prime();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lease?.Dispose();
                lease = null;
            }
            base.Dispose(disposing);
        }

        void EnsureLease()
        {
            if (isLeaseCreated)
                return;
            isLeaseCreated = true;
            if (string.IsNullOrWhiteSpace(effect.FilePath))
                return;
            try
            {
                lease = Vst3InstancePool.AcquireProcessing(effect, Hz);
                inputFramePosition = position / 2;
                Prime();
            }
            catch
            {
                lease = null;
            }
        }

        void Prime()
        {
            if (Input is null || lease is null)
                return;
            var plugin = lease.Instance;
            var latency = plugin.LatencySamples;
            tailRemaining = latency * 2L;
            delayIndex = 0;
            if (latency <= 0)
            {
                delayLine = [];
                return;
            }
            if (delayLine.Length == latency * 2)
                Array.Clear(delayLine);
            else
                delayLine = new float[latency * 2];
            if (primeBuffer.Length < BlockFrames * 2)
                primeBuffer = new float[BlockFrames * 2];
            var transport = CreateTransport();
            var written = 0;
            var remaining = latency;
            while (remaining > 0)
            {
                var frames = Math.Min(remaining, BlockFrames);
                var samples = frames * 2;
                var read = Input.Read(primeBuffer, 0, samples);
                read -= read % 2;
                if (read < samples)
                    Array.Clear(primeBuffer, read, samples - read);
                Array.Copy(primeBuffer, 0, delayLine, written, samples);
                plugin.Process(primeBuffer, 0, frames, inputFramePosition, transport);
                inputFramePosition += frames;
                written += samples;
                remaining -= frames;
            }
        }

        Vst3Transport CreateTransport() =>
            new(effect.Tempo, effect.TimeSignatureNumerator, effect.TimeSignatureDenominator, effect.IsTempoSyncEnabled);
    }
}
