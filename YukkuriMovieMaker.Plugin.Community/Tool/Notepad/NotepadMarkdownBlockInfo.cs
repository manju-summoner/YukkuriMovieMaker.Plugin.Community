namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed record NotepadMarkdownBlockInfo(
        NotepadMarkdownBlockType BlockType,
        int HeadingLevel,
        int MarkerLength,
        bool TaskChecked)
    {
        public static NotepadMarkdownBlockInfo Normal { get; } =
            new(NotepadMarkdownBlockType.Normal, 0, 0, false);
    }
}
