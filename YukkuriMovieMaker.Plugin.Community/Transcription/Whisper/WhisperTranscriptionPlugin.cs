using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using Whisper.net;
using YukkuriMovieMaker.Plugin.Community.Transcription.Whisper.Installers;
using YukkuriMovieMaker.Plugin.Transcription;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    internal class WhisperTranscriptionPlugin : ITranscriptionPlugin
    {
        readonly HashSet<string> ngWords = ["あ", "ん", "ご視聴ありがとうございました"];
        const int blockSeconds = 30;
        const int sampleRate = 16000;

        public string Name => Texts.Whisper;

        public UIElement CreateSettingsView() => new WhisperTranscriptionSettingsView();

        public TranscriptionSupportedAudioFormat GetSupportedAudioFormat() => new() { SampleRate = sampleRate, Channels = 1 };

        public async IAsyncEnumerable<TranscriptionSegment> ProcessAsync(ITranscriptionProcessArgs args, [EnumeratorCancellation] CancellationToken token)
        {
            var progress = args.ProgressMessage;
            await CheckRuntime(progress, token);

            var model = WhisperTranscriptionSettings.Default.Model;
            var modelPath = model.GetFilePath(WhisperModels.ModelDirectory);
            if (!File.Exists(modelPath) && !string.IsNullOrEmpty(model.URL))
                await model.DownloadAsync(WhisperModels.ModelDirectory, args.ProgressMessage, token);

            var language = WhisperTranscriptionSettings.Default.Language;

            progress.Report(-1, Texts.CreatingAudioStreamMessage);
            using var source = await Task.Run(args.CreateAudioStream, token);

            progress.Report(-1, Texts.LoadingWhisperModelMessage);
            using var factory = await CreateFactoryAsync(modelPath, token);
            var whisperBuider =
                factory
                .CreateBuilder()
                .WithThreads(Environment.ProcessorCount)
                .WithPrintTimestamps();
            if (language.IsAuto)
                whisperBuider.WithLanguageDetection();
            else
                whisperBuider.WithLanguage(language.Code);
            await using var whisper = whisperBuider.Build();

            var blockBuffer = new float[sampleRate * blockSeconds];

            var lastText = string.Empty;
            var readCount = 0;
            long blockPosition = source.Position;
            var blockDuration = TimeSpan.FromSeconds(blockSeconds);
            var totalSamples = source.Duration;
            var sourceDuration = TimeSpan.FromSeconds((double)totalSamples / sampleRate);

            //音声データ全体を処理するとメモリを使用量が爆増するため、30秒ごとのブロックに分けて処理する
            while ((readCount = await Task.Run(() => source.Read(blockBuffer, 0, blockBuffer.Length), token)) > 0)
            {
                var blockTime = TimeSpan.FromSeconds((double)blockPosition / sampleRate);
                string message = CreateProgressMessage(lastText, sourceDuration, blockTime);
                progress.Report((double)blockPosition / totalSamples, message);

                List<SegmentData> segments = [];
                await foreach (var segment in whisper.ProcessAsync(blockBuffer[0..readCount], token))
                {
                    if (ngWords.Contains(segment.Text))
                        continue;
                    segments.Add(segment);

                    lastText = segment.Text;
                    message = CreateProgressMessage(lastText, sourceDuration, blockTime);
                    progress.Report((double)blockPosition / totalSamples, message);
                }

                //ブロックの末尾にセグメントの末尾がある場合、セグメントの途中にブロックの切り替えが発生したと見なし、削除する
                if (segments.Count > 1 && blockDuration.TotalSeconds - segments.Last().End.TotalSeconds < 0.5)
                    segments.RemoveAt(segments.Count - 1);

                if (segments.Count > 0)
                {
                    //アイテムの末尾の部分まで巻き戻す
                    //ブロックの区切りで読み取れなかったセリフや、途中で区切られてしまったセリフを次のブロックで読み取ることを期待する。
                    var targetTime = segments.Max(x => blockTime + x.End);
                    var position = (long)(targetTime.TotalSeconds * sampleRate);
                    source.Seek(position);
                }

                foreach (var segment in segments.Select(x => new TranscriptionSegment() { Start = blockTime + x.Start, End = blockTime + x.End, Text = x.Text }))
                    yield return segment;

                Array.Clear(blockBuffer);
                blockPosition = source.Position;
            }
        }

        static async Task<WhisperFactory> CreateFactoryAsync(string filePath, CancellationToken token)
        {
            //ファイルパスにマルチバイト文字が含まれているかどうかをチェック
            if (filePath.Any(c => c > 127))
            {
                //マルチバイト文字を含むファイルはWhisperFactory.FromPath()で読み込むことが出来ない
                //MemoryMappedFile経由でMemory化してからWhisperFactory.FromBuffer()で読み込む
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > int.MaxValue)
                    throw new NotSupportedException(Texts.FileSizeLimitMessage);
                using var mm = new MemoryMappedFileMemoryManager(filePath);
                var factory = await Task.Run(() => WhisperFactory.FromBuffer(mm.Memory), token);
                return factory;
            }
            else
            {
                //マルチバイト文字を含まないファイルはWhisperFactory.FromPath()で直接読み込む
                return await Task.Run(() => WhisperFactory.FromPath(filePath), token);
            }
        }

        private static async Task CheckRuntime(YukkuriMovieMaker.Commons.ProgressMessage progress, CancellationToken token)
        {
            var isCudaRuntimeInstalled = CUDAToolkit.IsInstalled();
            var isVulkanRuntimeInstalled = VulkanRuntime.IsInstalled();
            if (isCudaRuntimeInstalled || isVulkanRuntimeInstalled)
                return;

            var vendor = GpuVendorDetector.GetGpuVendor();
            if (CUDAToolkit.IsSupported(vendor))
            {
                var res = MessageBox.Show(string.Format(Texts.RuntimeNotInstalledMessage, $"CUDA Toolkit ({RuntimeInformation.ProcessArchitecture})"), Texts.RuntimeNotInstalledTitle, MessageBoxButton.YesNoCancel);
                if (res is MessageBoxResult.Cancel)
                    throw new OperationCanceledException();
                if (res is MessageBoxResult.Yes)
                {
                    await new CUDAToolkit().InstallAsync(progress, token);
                    return;
                }
            }
            else if (VulkanRuntime.IsSupported(vendor))
            {
                var res = MessageBox.Show(string.Format(Texts.RuntimeNotInstalledMessage, $"Vulkan Runtime ({RuntimeInformation.ProcessArchitecture})"), Texts.RuntimeNotInstalledTitle, MessageBoxButton.YesNoCancel);
                if (res is MessageBoxResult.Cancel)
                    throw new OperationCanceledException();
                if (res is MessageBoxResult.Yes)
                {
                    await new VulkanRuntime().InstallAsync(progress, token);
                    return;
                }
            }

            if (!VisualCppRuntime.IsSupported())
                throw new NotSupportedException(Texts.NotSupportedMessage);

            if (!VisualCppRuntime.IsInstalled())
            {
                var res = MessageBox.Show(string.Format(Texts.RuntimeNotInstalledMessage, $"Microsoft Visual C++ 2015–2022 Redistributable ({RuntimeInformation.ProcessArchitecture})"), Texts.RuntimeNotInstalledTitle, MessageBoxButton.YesNoCancel);
                if (res is MessageBoxResult.Cancel)
                    throw new OperationCanceledException();
                if (res is MessageBoxResult.Yes)
                {
                    await new VisualCppRuntime().InstallAsync(progress, token);
                }
            }
        }

        private static string CreateProgressMessage(string lastText, TimeSpan totalTime, TimeSpan blockTime)
        {
            var timeSpanFormat = @"hh\:mm\:ss\.ff";
            return string.Format(
                Texts.ProcessingAudioMessage,
                blockTime.ToString(timeSpanFormat),
                totalTime.ToString(timeSpanFormat),
                lastText);
        }
    }
}
