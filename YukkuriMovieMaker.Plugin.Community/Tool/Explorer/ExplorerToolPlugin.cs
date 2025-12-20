using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerToolPlugin : IToolPlugin
    {
        public Type ViewModelType => typeof(ExplorerViewModel);

        public Type ViewType => typeof(ExplorerView);

        public string Name => Texts.Explorer;
        public bool AllowMultipleInstances => true;
    }
}
