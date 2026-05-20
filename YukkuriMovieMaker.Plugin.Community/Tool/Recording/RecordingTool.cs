namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    internal class RecordingTool : IToolPlugin
    {
        public string Name => "録音ツール";

        public Type ViewModelType => typeof(ToolViewModel);
        public Type ViewType => typeof(ToolView);
    }
}

