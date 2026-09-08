using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    /// <summary>
    /// 参照ファイルを動画として扱うかの判定。
    /// 再生開始位置の表示条件・読み込みで先に試すソース・リソース一覧の種別が同じ規則を共有する。
    /// </summary>
    internal static class ColorTransferFileKind
    {
        public static bool IsVideo(string filePath)
            => (FileSettings.Default.FileExtensions.GetFileType(filePath) & FileType.動画) != 0;
    }
}
