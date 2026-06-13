namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public sealed class AddressBarMenuEntry(string displayText, string fullPath)
    {
        public string DisplayText { get; } = displayText ?? string.Empty;
        public string FullPath { get; } = fullPath ?? string.Empty;
    }
}
