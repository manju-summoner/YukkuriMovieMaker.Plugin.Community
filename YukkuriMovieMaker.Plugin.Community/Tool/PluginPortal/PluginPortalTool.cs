namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalTool : IToolPlugin
    {
        public string Name => Texts.PluginPortal;

        public Type ViewModelType => typeof(PluginPortalViewModel);
        public Type ViewType => typeof(PluginPortalView);
    }
}
