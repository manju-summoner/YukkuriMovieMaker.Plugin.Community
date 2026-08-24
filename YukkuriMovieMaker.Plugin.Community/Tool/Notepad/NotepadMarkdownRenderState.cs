namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadMarkdownRenderState
    {
        public NotepadMarkdownDocumentMap DocumentMap { get; set; } = new();
        public int ActiveLineNumber { get; set; } = -1;
    }
}
