using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.InstantLoopAudioEffect
{
    internal class InstantLoopAudioEffectProcessor(InstantLoopAudioEffect item) : AudioEffectProcessorBase
    {
        const int Channels = 2;

        readonly List<float> loopBuffer = [];
        long loopBufferPosition;

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => Input?.Duration ?? 0;

        protected override void seek(long outputPos)
        {
            if (Input is null) return;

            loopBuffer.Clear();
            var info = Resolve(outputPos / Channels);
            if (!info.IsGap)
                Input.Seek(info.SourceFrame * Channels);
        }
        protected override int read(float[] buffer, int offset, int count)
        {
            if (Input is null) return 0;

            //奇数サンプルを書き出すとPositionが奇数に進み、以降のL/Rペアリングとフレーム計算がずれるため偶数に丸める
            count -= count % Channels;

            int written = 0;

            while (written < count)
            {
                int remaining = count - written;
                long currentFrame = (Position + written) / Channels;
                var info = Resolve(currentFrame);

                long maxFrames = info.FramesUntilBoundary == long.MaxValue
                    ? (long)(remaining / Channels)
                    : Math.Min(info.FramesUntilBoundary, (long)(remaining / Channels));
                int chunkFloats = (int)(maxFrames * Channels);

                if (chunkFloats <= 0)
                {
                    if (remaining > 0)
                    {
                        Array.Clear(buffer, offset + written, remaining);
                        written += remaining;
                    }
                    break;
                }

                if (info.IsGap)
                {
                    Array.Clear(buffer, offset + written, chunkFloats);
                    written += chunkFloats;
                }
                else
                {
                    long inputPosition = info.SourceFrame * Channels;
                    int readCount = info.IsLoop
                        ? ReadLoop(inputPosition, buffer, offset + written, chunkFloats)
                        : ReadInput(inputPosition, buffer, offset + written, chunkFloats);
                    if (readCount <= 0)
                    {
                        //ソース終端に達した場合もこのチャンク分だけ無音化して続行する
                        //（次のサイクルで巻き戻せばまだ音声が存在する可能性がある）
                        Array.Clear(buffer, offset + written, chunkFloats);
                        written += chunkFloats;
                        continue;
                    }
                    written += readCount;
                }
            }

            return written;
        }

        int ReadInput(long position, float[] buffer, int offset, int count)
        {
            if (Input!.Position != position)
                Input.Seek(position);

            return Input.Read(buffer, offset, count);
        }

        int ReadLoop(long position, float[] buffer, int offset, int count)
        {
            if (position < loopBufferPosition || loopBufferPosition + loopBuffer.Count < position)
            {
                loopBuffer.Clear();
                loopBufferPosition = position;
            }

            int bufferOffset = (int)(position - loopBufferPosition);
            int cachedCount = Math.Min(count, loopBuffer.Count - bufferOffset);
            loopBuffer.CopyTo(bufferOffset, buffer, offset, cachedCount);
            if (cachedCount == count)
                return count;

            int readCount = ReadInput(position + cachedCount, buffer, offset + cachedCount, count - cachedCount);
            if (readCount <= 0)
                return cachedCount;

            loopBuffer.AddRange(buffer.AsSpan(offset + cachedCount, readCount));
            return cachedCount + readCount;
        }

        readonly record struct FrameInfo(long SourceFrame, bool IsGap, long FramesUntilBoundary)
        {
            public bool IsLoop => FramesUntilBoundary != long.MaxValue;
        }

        FrameInfo Resolve(long currentFrame)
        {
            long totalFrames = Duration / Channels;
            long startOffsetFrames = MsToFrames(item.StartOffset.GetValue(currentFrame, totalFrames, Hz));
            long intervalFrames = Math.Max(1L, MsToFrames(item.Interval.GetValue(currentFrame, totalFrames, Hz)));
            long gapFrames = Math.Max(0L, MsToFrames(item.Gap.GetValue(currentFrame, totalFrames, Hz)));
            int repeatCountInt = (int)Math.Round(item.RepeatCount.GetValue(currentFrame, totalFrames, Hz));
            bool infinite = repeatCountInt <= 0;
            long cycleFrames = intervalFrames + gapFrames;
            //初回再生はエフェクトなしでも聞こえる分なので、指定回数ぶん巻き戻すには+1サイクル必要
            long loopEndFrame = infinite ? long.MaxValue : (repeatCountInt + 1L) * cycleFrames;

            if (infinite || currentFrame < loopEndFrame)
            {
                long posInCycle = currentFrame % cycleFrames;
                if (posInCycle < intervalFrames)
                {
                    long toEndOfPlay = intervalFrames - posInCycle;
                    long framesUntilBoundary = infinite
                        ? toEndOfPlay
                        : Math.Min(toEndOfPlay, loopEndFrame - currentFrame);
                    return new FrameInfo(startOffsetFrames + posInCycle, false, framesUntilBoundary);
                }
                else
                {
                    long toEndOfGap = cycleFrames - posInCycle;
                    long framesUntilBoundary = infinite
                        ? toEndOfGap
                        : Math.Min(toEndOfGap, loopEndFrame - currentFrame);
                    return new FrameInfo(-1, true, framesUntilBoundary);
                }
            }
            else
            {
                long pastLoopFrames = currentFrame - loopEndFrame;
                return new FrameInfo(startOffsetFrames + intervalFrames + pastLoopFrames, false, long.MaxValue);
            }
        }

        long MsToFrames(double ms) => (long)(ms / 1000.0 * Hz);
    }
}