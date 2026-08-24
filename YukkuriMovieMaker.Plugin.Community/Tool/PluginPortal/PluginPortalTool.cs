namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalTool : IToolPlugin
    {
        public string Name => Texts.PluginPortal;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.ToolGroupUtilityName;
        public int DefaultOrder => 530;

        public Type ViewModelType => typeof(PluginPortalViewModel);
        public Type ViewType => typeof(PluginPortalView);
    }
}
