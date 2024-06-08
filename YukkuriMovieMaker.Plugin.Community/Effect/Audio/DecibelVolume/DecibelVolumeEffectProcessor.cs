using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.DecibelVolume
{
    internal class DecibelVolumeEffectProcessor(DecibelVolumeEffect item) : AudioEffectProcessorBase
    {
        public override int Hz => Input?.Hz ?? throw new InvalidOperationException();

        public override long Duration => Input?.Duration ?? throw new InvalidOperationException();

        long currentPosition = 0;

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if(Input is null)
                return 0;
            var read = Input.Read(destBuffer, offset, count);
            for (int i = 0; i < read; i++)
            {
                var dB = item.Decibel.GetValue((currentPosition + i) % 2, Duration % 2, Hz);
                var gain = dB <= -60 ? 0 : (float)Math.Pow(10, dB / 20);
                destBuffer[offset + i] *= gain;
            }
            currentPosition += read;
            return read;
        }

        protected override void seek(long position)
        {
            currentPosition = position;
            Input?.Seek(position);
        }
    }
}