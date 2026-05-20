using System.ComponentModel;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.ItemEditor;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoicePronounce : IVoicePronounce, IEditable, INotifyPropertyChanged, IUndoRedoable
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<UndoRedoEventArgs>? UndoRedoCommandCreated
        {
            add { }
            remove { }
        }

        public IVoicePronounce Clone()
        {
            return new RecordedVoicePronounce();
        }

        public void BeginEdit()
        {
        }

        public ValueTask EndEditAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}


