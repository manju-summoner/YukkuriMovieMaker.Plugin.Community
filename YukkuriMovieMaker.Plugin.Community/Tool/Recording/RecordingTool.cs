namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    internal class RecordingTool : IToolPlugin
    {
        public string Name => Texts.ToolName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.ToolGroupVoiceName;
        public int DefaultOrder => 310;

        public Type ViewModelType => typeof(ToolViewModel);
        public Type ViewType => typeof(ToolView);
    }
}

