namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadImageInsertRequestedEventArgs(string filePath) : EventArgs
    {
        public string FilePath { get; } = filePath;
    }
}
