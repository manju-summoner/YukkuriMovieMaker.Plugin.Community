using System;
using System.Diagnostics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXホストのログ出力。
    /// プラグインからのコールバック内では例外を外へ漏らせないため、ログは失敗しても握りつぶす。
    /// </summary>
    internal static class OfxHostLog
    {
        /// <summary>テストやデバッグでログを横取りするためのフック</summary>
        public static event Action<string>? MessageLogged;

        /// <summary>
        /// Infoの出力先（YMM4本体ではLog.Defaultへ流す。OpenFxSettings.Initializeで設定される）。
        /// スキャナー子プロセスでは未設定のままにする（Log.Defaultを触るとスキャナーEXEの隣に
        /// ログフォルダーが作られてしまうため）
        /// </summary>
        public static Action<string>? Sink { get; set; }

        [Conditional("DEBUG")]
        public static void Debug(string message)
        {
            Trace.WriteLine($"[OpenFX] {message}");
            SafeInvoke(message);
        }

        public static void Info(string message)
        {
            Trace.WriteLine($"[OpenFX] {message}");
            try
            {
                Sink?.Invoke($"[OpenFX] {message}");
            }
            catch
            {
                // ログ出力の失敗はネイティブ境界へ漏らさない
            }
            SafeInvoke(message);
        }

        static void SafeInvoke(string message)
        {
            try
            {
                MessageLogged?.Invoke(message);
            }
            catch
            {
                // ログ購読者の例外はネイティブ境界へ漏らさない
            }
        }
    }
}
