using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    public class TypeFilterItem(string name, bool isChecked, Action filterAction) : Bindable
    {
        public string Name { get; } = name;
        
        private bool _isChecked;
        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    filterAction?.Invoke();
                }
            }
        }
    }
}
