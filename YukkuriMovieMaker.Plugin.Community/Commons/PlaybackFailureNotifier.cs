using System;
using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Commons
{
    /// <summary>
    /// 試聴の再生を開始できなかったことを利用者に知らせます。
    /// AudioPlayer は再生デバイスを開けないと例外を投げるため、ログだけで済ませると音が出ない理由が利用者に伝わらない
    /// </summary>
    internal static class PlaybackFailureNotifier
    {
#if DEBUG
        /// <summary>テスト用。メッセージボックスの代わりに (message, caption) で呼ばれる</summary>
        internal static Action<string, string>? OverrideForTest;
#endif

        /// <param name="owner">ダイアログの親にするウィンドウ。null ならメインウィンドウを親にする</param>
        public static void Notify(string message, string caption, Window? owner = null)
        {
#if DEBUG
            if (OverrideForTest is { } overrideForTest)
            {
                overrideForTest(message, caption);
                return;
            }
#endif
            //音声生成の待ちの間に他のアプリへ切り替えられていると、親無しのダイアログはYMM4の背面に隠れて再生ボタンも押せてしまう
            owner ??= Application.Current?.MainWindow;
            if (owner is not null)
                MessageBox.Show(owner, message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
