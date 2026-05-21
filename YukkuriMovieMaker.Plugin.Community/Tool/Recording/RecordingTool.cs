namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    internal class RecordingTool : IToolPlugin
    {
        public string Name => Texts.ToolName;

        public Type ViewModelType => typeof(ToolViewModel);
        public Type ViewType => typeof(ToolView);
    }
}

