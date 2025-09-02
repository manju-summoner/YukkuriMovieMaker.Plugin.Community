using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class SelectablePluginFile(string filePath) : Bindable
    {
        private bool _isSelected = true;

        public string FilePath { get; } = filePath;
        public string FileName { get; } = Path.GetFileName(filePath);

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }
    }
}
