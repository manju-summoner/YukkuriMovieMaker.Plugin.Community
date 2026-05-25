using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    internal class RecordedVoiceAudioSelectorEditorAttribute : PropertyEditorAttribute
    {
        public override FrameworkElement Create()
        {
            return new RecordedVoiceAudioSelectorEditor();
        }

        public override void SetBindings(FrameworkElement control, object item, object propertyOwner, PropertyInfo propertyInfo)
        {
            if (control is not RecordedVoiceAudioSelectorEditor editor)
                return;
            if (propertyOwner is not RecordedVoiceParameter parameter)
                return;

            editor.Parameter = parameter;
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not RecordedVoiceAudioSelectorEditor editor)
                return;

            editor.Parameter = null;
        }
    }
}

