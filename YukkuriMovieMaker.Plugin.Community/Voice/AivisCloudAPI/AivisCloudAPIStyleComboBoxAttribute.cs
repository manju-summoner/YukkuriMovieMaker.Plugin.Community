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
        VoiceDescription? voiceDescription;
        public VoiceDescription? VoiceDescription
        {
            get => voiceDescription;
            set => Set(ref voiceDescription, value);
        }

        public override FrameworkElement Create()
        {
            return new CommonComboBox();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not CommonComboBox comboBox)
                return;
            if (VoiceDescription?.Speaker is not AivisCloudAPIVoiceSpeaker speaker)
                return;

            comboBox.ItemsSource = speaker.Styles;
            comboBox.DisplayMemberPath = nameof(StyleContract.Name);
            comboBox.SelectedValuePath = nameof(StyleContract.LocalId);
            comboBox.SetBinding(CommonComboBox.ValueProperty, ItemPropertiesBinding.Create(itemProperties));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not CommonComboBox comboBox)
                return;
            BindingOperations.ClearBinding(comboBox, CommonComboBox.ValueProperty);
            comboBox.ItemsSource = null;
            comboBox.DisplayMemberPath = null;
            comboBox.SelectedValuePath = null;
        }
    }
}