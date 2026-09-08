using Whisper.net.Logger;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    /// <summary>
    /// whisper.cpp / ggml が whisper_log_set 経由で出すログと、Whisper.net 自身のログの直近分を保持する。
    /// ネイティブ側の C++ 例外は P/Invoke 境界で SEHException に変わり原因の文字列が失われるため、
    /// 失敗の直前に出たログを利用者への案内とローカルログに添える。
    /// Whisper.net はネイティブのレベルを Error / Warning / それ以外＝Info に丸めて渡すため、
    /// WhisperLogLevel.Cont はここには届かず、ggml の CONT も Info の別行として扱う（連結処理は持たない）。
    /// Debug はランタイム選択（CPU / CUDA / Vulkan のどれを読み込んだか）などマネージド側のログにだけ使われ、
    /// プロセス内の初回にしか流れない。
    /// </summary>
    internal sealed class WhisperNativeLogBuffer
    {
        //モデル読み込みから推論までの初期化ログ（数十行）を失敗時にまとめて残せる量
        public const int Capacity = 50;
        public const int ErrorLineCapacity = 5;

        readonly Lock gate = new();
        readonly List<(WhisperLogLevel Level, string Text)> lines = [];
        readonly Action<string> warningSink;

        /// <param name="warningSink">
        /// Warning 以上のログを受け取る（ローカルログへの即時書き出し用）。
        /// ネイティブのコールバックスレッドから呼ばれ、例外が抜けるとプロセスが落ちるため、例外を投げてはならない。
        /// </param>
        public WhisperNativeLogBuffer(Action<string> warningSink)
        {
            this.warningSink = warningSink;
        }

        /// <summary>
        /// LogProvider.AddLogger に渡すコールバック。ネイティブ側の推論スレッドから呼ばれる。
        /// </summary>
        public void Append(WhisperLogLevel level, string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            var text = message.Trim('\r', '\n');

            lock (gate)
            {
                lines.Add((level, text));
                if (lines.Count > Capacity)
                    lines.RemoveAt(0);
            }

            if (level is WhisperLogLevel.Error or WhisperLogLevel.Warning)
                warningSink(text);
        }

        /// <summary>
        /// 直近のログ全行（Info を含む）。ローカルログに残す診断用。
        /// </summary>
        public string GetRecentLines()
        {
            lock (gate)
                return string.Join(Environment.NewLine, lines.Select(x => x.Text));
        }

        /// <summary>
        /// 直近の Warning 以上の行。利用者への案内に添える。
        /// </summary>
        public string GetRecentErrorLines()
        {
            lock (gate)
                return string.Join(
                    Environment.NewLine,
                    lines
                        .Where(x => x.Level is WhisperLogLevel.Error or WhisperLogLevel.Warning)
                        .TakeLast(ErrorLineCapacity)
                        .Select(x => x.Text));
        }

        /// <summary>
        /// 案内文にネイティブログの Warning 以上の行を添える。該当行が無ければ案内文のみ。
        /// </summary>
        public string CreateFailureMessage(string header)
        {
            var errorLines = GetRecentErrorLines();
            return errorLines.Length == 0
                ? header
                : $"{header}{Environment.NewLine}{Environment.NewLine}{errorLines}";
        }
    }
}
