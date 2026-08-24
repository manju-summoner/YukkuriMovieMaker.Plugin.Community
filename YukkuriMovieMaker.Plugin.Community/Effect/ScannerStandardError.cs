using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Effect
{
    /// <summary>
    /// 隔離スキャナー側で診断情報を付与済みの失敗を表す。
    /// </summary>
    internal sealed class ScannerFailureException : InvalidOperationException
    {
        public ScannerFailureException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// 隔離スキャナーの標準エラーを、パイプを最後まで排出しながら上限付きで保持する。
    /// </summary>
    internal static class ScannerStandardError
    {
        internal const int MaximumCapturedBytes = 4096;
        static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

        public static async Task<string> CaptureAsync(Stream stream)
        {
            var buffer = new byte[4096];
            using var captured = new MemoryStream(MaximumCapturedBytes);
            var truncated = false;
            try
            {
                while (true)
                {
                    var length = await stream.ReadAsync(buffer).ConfigureAwait(false);
                    if (length == 0)
                        break;

                    var remaining = MaximumCapturedBytes - (int)captured.Length;
                    if (remaining > 0)
                        captured.Write(buffer, 0, Math.Min(remaining, length));
                    if (length > remaining)
                        truncated = true;
                }
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // プロセス終了・破棄と同時にパイプが閉じられた場合は、それまでの内容を使用する
            }

            var bytes = captured.ToArray();
            var decoder = Encoding.UTF8.GetDecoder();
            var characters = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            // 上限がUTF-8の途中に当たった場合、末尾の不完全なシーケンスはflushせず破棄する。
            var characterCount = decoder.GetChars(bytes, 0, bytes.Length, characters, 0, flush: false);
            var text = new string(characters, 0, characterCount).Trim();
            if (truncated)
                text += $"…（先頭{MaximumCapturedBytes}バイトまで）";
            return text;
        }

        public static string AppendToReason(string reason, Task<string> standardErrorTask)
            => AppendToReason(reason, standardErrorTask, CompletionTimeout);

        internal static string AppendToReason(string reason, Task<string> standardErrorTask, TimeSpan completionTimeout)
        {
            try
            {
                if (!standardErrorTask.Wait(completionTimeout))
                    return $"{reason} stderr=(取得できませんでした)";
                var standardError = standardErrorTask.Result;
                return string.IsNullOrWhiteSpace(standardError)
                    ? reason
                    : $"{reason} stderr={standardError}";
            }
            catch (Exception e) when (e is AggregateException or InvalidOperationException)
            {
                return $"{reason} stderr=(取得できませんでした)";
            }
        }
    }
}
