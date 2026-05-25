using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public class RenameRecordViewModel : Bindable
    {
        string newName;
        public string NewName
        {
            get => newName;
            set
            {
                if (Set(ref newName, value ?? string.Empty))
                    (OkCommand as ActionCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool Confirmed { get; private set; }

        public ICommand OkCommand { get; }

        public RenameRecordViewModel(string initialName)
        {
            newName = initialName ?? string.Empty;
            OkCommand = new ActionCommand(
                _ => !string.IsNullOrWhiteSpace(NewName),
                _ => Confirmed = true);
        }
    }
}
