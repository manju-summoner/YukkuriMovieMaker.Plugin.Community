
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI
{
    internal class AivisCloudAPIStyleComboBoxAttribute : PropertyEditorAttribute2, IPropertyEditorForVoiceParameterAttribute
    {
        public VoiceDescription? VoiceDescription { get; set; }


        public override FrameworkElement Create()
        {
            return new CommonComboBox();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if(control is not CommonComboBox comboBox)
                throw new InvalidOperationException("Control must be of type CommonComboBox.");
            if (VoiceDescription?.Speaker is not AivisCloudAPIVoiceSpeaker speaker)
                throw new InvalidOperationException("VoiceDescription must have a valid AivisSpeechVoiceSpeaker.");

            comboBox.ItemsSource = speaker.Styles;
            comboBox.DisplayMemberPath = nameof(StyleContract.Name);
            comboBox.SelectedValuePath = nameof(StyleContract.LocalId);
            comboBox.SetBinding(CommonComboBox.ValueProperty, ItemPropertiesBinding.Create(itemProperties));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not CommonComboBox comboBox)
                throw new InvalidOperationException("Control must be of type CommonComboBox.");
            BindingOperations.ClearBinding(comboBox, CommonComboBox.ValueProperty);
            comboBox.ItemsSource = null;
            comboBox.DisplayMemberPath = null;
            comboBox.SelectedValuePath = null;
        }
    }
}