using System;
using System.Collections.Generic;
using System.Text;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class NotepadToolPlugin : IToolPlugin
    {
        public Type ViewModelType => typeof(NotepadViewModel);

        public Type ViewType => typeof(NotepadView);

        public string Name => Texts.Notepad;
        public bool AllowMultipleInstances => true;
    }
}
