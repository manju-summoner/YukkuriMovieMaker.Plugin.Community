using System.IO;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Commons;
using YukkuriMovieMaker.Plugin.VoiceActivityDetector;

namespace YukkuriMovieMaker.Plugin.Community.VoiceActivityDetector.SileroVad
{
    internal class SileroVoiceDetector(float threshold, int minSpeechDurationMs, float maxSpeechDurationSeconds, int minSilenceDurationMs, int speechPadMs) : Bindable, IVoiceActivityDetector
    {
        static readonly string modelFilePath = Path.Combine(AppDirectories.ResourceDirectory, "models", "silero", "silero_vad_v6.onnx");
        SileroVadDetector? vad;

        public async Task DownloadResourcesAsync(ProgressMessage progress, CancellationToken token)
        {
            if (File.Exists(modelFilePath))
                return;

            //silero vad v6 model
            var modelUrl = "https://raw.githubusercontent.com/snakers4/silero-vad/4c00cd14be0ff5b8bd6846a6eec72741aac837f2/src/silero_vad/data/silero_vad.onnx";
            await Downloader.DownloadAsync(modelUrl, modelFilePath, progress, token);
        }

        public async Task InitializeAsync(CancellationToken token)
        {
            vad = await Task.Run(() => new SileroVadDetector(
                modelFilePath,
                threshold / 100,
                16000,
                minSpeechDurationMs,
                maxSpeechDurationSeconds,
                minSilenceDurationMs,
                speechPadMs));
        }

        public async IAsyncEnumerable<VoiceDetectorSegment> ProcessAsync(ReadOnlyMemory<float> samples, [EnumeratorCancellation] CancellationToken token)
        {
            if (vad is null)
                yield break;

            var result = await Task.Run(() => vad.GetSpeechSegmentList(samples.Span), token);
            foreach(var seg in result)
                yield return new VoiceDetectorSegment(seg.StartOffset, seg.EndOffset);
        }

        public void Dispose()
        {
            vad?.Dispose();
        }
    }
}
