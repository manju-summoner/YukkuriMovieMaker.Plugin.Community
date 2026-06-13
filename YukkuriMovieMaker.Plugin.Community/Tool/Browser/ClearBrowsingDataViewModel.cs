using System;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal class ClearBrowsingDataViewModel : Bindable
    {
        public bool ClearBrowserCache
        {
            get;
            set
            {
                if (Set(ref field, value))
                    ((ActionCommand)ClearCommand).RaiseCanExecuteChanged();
            }
        } = true;

        public bool ClearPluginHistory
        {
            get;
            set
            {
                if (Set(ref field, value))
                    ((ActionCommand)ClearCommand).RaiseCanExecuteChanged();
            }
        } = true;

        public bool ClearPluginFavicon
        {
            get;
            set
            {
                if (Set(ref field, value))
                    ((ActionCommand)ClearCommand).RaiseCanExecuteChanged();
            }
        } = true;

        public ICommand ClearCommand { get; }

        public ClearBrowsingDataViewModel(Action<ClearBrowsingDataViewModel> onClear)
        {
            ClearCommand = new ActionCommand(
                _ => ClearBrowserCache || ClearPluginHistory || ClearPluginFavicon,
                _ => onClear(this));
        }
    }
}
