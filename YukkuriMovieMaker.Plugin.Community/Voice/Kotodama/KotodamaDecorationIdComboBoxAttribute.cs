
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    internal class KotodamaDecorationIdComboBoxAttribute : PropertyEditorAttribute2, IPropertyEditorForVoiceParameterAttribute
    {
        public VoiceDescription? VoiceDescription { get; set => Set(ref field, value); }

        public override FrameworkElement Create()
        {
            return new CommonComboBox();
        }
        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if(control is not CommonComboBox comboBox)
                return;
            if(VoiceDescription?.Speaker is not KotodamaVoiceSpeaker speaker)
                return;

            comboBox.ItemsSource = speaker.GetDecorations();
            comboBox.DisplayMemberPath = nameof(KotodamaDecorationSettings.Name);
            comboBox.SelectedValuePath = nameof(KotodamaDecorationSettings.DecorationId);
            comboBox.SetBinding(CommonComboBox.ValueProperty, ItemPropertiesBinding.Create2(itemProperties));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if(control is not CommonComboBox comboBox)
                return;
            BindingOperations.ClearBinding(comboBox, CommonComboBox.ValueProperty);
            comboBox.ItemsSource = null;
        }
    }
}