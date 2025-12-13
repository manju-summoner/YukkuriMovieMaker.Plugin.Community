namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class NotepadState
    {
        public string FilePath { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public double Zoom { get; set; } = 1.0;
        public bool IsSaved { get; set; } = true;
    }
}
