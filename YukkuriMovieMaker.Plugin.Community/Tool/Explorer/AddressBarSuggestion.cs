namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public sealed class AddressBarSuggestion(string displayText, string insertText, string? fullPath, AddressBarSuggestionSource source)
    {
        /// <summary>
        /// 表示用テキスト
        /// </summary>
        public string DisplayText { get; } = displayText ?? string.Empty;
        /// <summary>
        /// 確定時に挿入されるテキスト（パス、%AppData%など）
        /// </summary>
        public string InsertText { get; } = insertText ?? string.Empty;
        /// <summary>
        /// パス
        /// </summary>
        public string? FullPath { get; } = fullPath;
        public AddressBarSuggestionSource Source { get; } = source;
    }
}
