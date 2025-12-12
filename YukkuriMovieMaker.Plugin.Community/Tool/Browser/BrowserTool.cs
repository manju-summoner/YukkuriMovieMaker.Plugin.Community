namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal class BrowserTool : IToolPlugin
    {
        public Type ViewModelType => typeof(BrowserViewModel);

        public Type ViewType => typeof(BrowserView);

        public string Name => Texts.Browser;

        public bool AllowMultipleInstances => true;
    }
}
