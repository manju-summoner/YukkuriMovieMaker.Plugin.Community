using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal class Vst3EffectProcessor(Vst3Effect effect) : AudioEffectProcessorBase
    {
        const int BlockFrames = 512;

        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();

        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        Vst3Plugin? plugin;
        bool isPluginCreated;
        bool isSubscribed;
        float[] dryBuffer = [];
        long position;

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null)
                return 0;
            count -= count % 2;
            var read = Input.Read(destBuffer, offset, count);
            read -= read % 2;

            EnsurePlugin();
            if (plugin is not null && read > 0)
            {
                if (dryBuffer.Length < read)
                    dryBuffer = new float[read];
                Array.Copy(destBuffer, offset, dryBuffer, 0, read);

                plugin.Process(destBuffer, offset, read / 2);

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

            position += read;
            return read;
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            this.position = position;
            plugin?.Reset();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (isSubscribed)
                {
                    effect.ParameterEdited -= OnParameterEdited;
                    isSubscribed = false;
                }
                plugin?.Dispose();
                plugin = null;
            }
            base.Dispose(disposing);
        }

        void OnParameterEdited(uint parameterId, double normalizedValue)
        {
            plugin?.QueueParameterChange(parameterId, normalizedValue);
        }

        void EnsurePlugin()
        {
            if (isPluginCreated)
                return;
            isPluginCreated = true;
            if (string.IsNullOrWhiteSpace(effect.FilePath))
                return;
            try
            {
                plugin = new Vst3Plugin(effect.FilePath, Hz, BlockFrames, DecodeState(effect.PluginState));
                effect.ParameterEdited += OnParameterEdited;
                isSubscribed = true;
            }
            catch
            {
                plugin = null;
            }
        }

        static byte[]? DecodeState(string state)
        {
            if (string.IsNullOrEmpty(state))
                return null;
            try
            {
                return Convert.FromBase64String(state);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
