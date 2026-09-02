using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows.Ink;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal static class PenToolFile
    {
        /// <summary>
        /// ペンツールの保存・読込用にファイルを開いて処理する。
        /// </summary>
        /// <param name="path">対象ファイル</param>
        /// <param name="mode">保存なら Create、読込なら Open</param>
        /// <param name="access">保存なら Write、読込なら Read。読込に書き込み権限を要求すると読み取り専用のファイルを開けなくなる</param>
        /// <param name="failedMessage">失敗時に利用者へ表示するメッセージの先頭行</param>
        /// <param name="action">開いたストリームに対する処理</param>
        /// <param name="errorMessage">失敗時の利用者向けメッセージ</param>
        /// <returns>処理が完了したら true</returns>
        public static bool TryOpen(string path, FileMode mode, FileAccess access, string failedMessage, Action<FileStream> action, [NotNullWhen(false)] out string? errorMessage)
        {
            try
            {
                using var stream = new FileStream(path, mode, access);
                action(stream);
                errorMessage = null;
                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                //コントロールされたフォルダーアクセスによる拒否、読み取り専用・隠しファイルへの上書き、他プロセスによる使用中など、
                //利用者側の環境が原因で起きる失敗はエラー報告ではなく案内で済ませる
                errorMessage = Report(failedMessage, path, e);
                return false;
            }
        }

        /// <summary>
        /// ISF ファイルからストロークを読み込む。
        /// </summary>
        /// <param name="path">対象ファイル</param>
        /// <param name="failedMessage">失敗時に利用者へ表示するメッセージの先頭行</param>
        /// <param name="strokes">読み込んだストローク</param>
        /// <param name="errorMessage">失敗時の利用者向けメッセージ</param>
        /// <returns>読み込めたら true</returns>
        public static bool TryLoadStrokes(string path, string failedMessage, [NotNullWhen(true)] out StrokeCollection? strokes, [NotNullWhen(false)] out string? errorMessage)
        {
            StrokeCollection? loaded = null;
            try
            {
                if (!TryOpen(path, FileMode.Open, FileAccess.Read, failedMessage, stream => loaded = new StrokeCollection(stream), out errorMessage))
                {
                    strokes = null;
                    return false;
                }
            }
            catch (ArgumentException e)
            {
                //0バイト・途中で切れた・別形式など ISF として解釈できないファイルは、利用者側のファイルの問題なので案内で済ませる
                errorMessage = Report(failedMessage, path, e);
                strokes = null;
                return false;
            }
            strokes = loaded!;
            errorMessage = null;
            return true;
        }

        static string Report(string failedMessage, string path, Exception e)
        {
            var message = $"{failedMessage}\r\n{path}\r\n{e.Message}";
            Log.Default.Write(message, e);
            return message;
        }
    }
}
