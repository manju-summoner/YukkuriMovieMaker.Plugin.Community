using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkVersionComboBoxAttribute : PropertyEditorAttribute2, IPropertyEditorForVoiceParameterAttribute
    {
        VoiceDescription? voiceDescription;
        public VoiceDescription? VoiceDescription { get => voiceDescription; set => Set(ref voiceDescription, value); }

        public override FrameworkElement Create()
        {
            return new CommonComboBox();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not CommonComboBox comboBox)
                return;
            if (VoiceDescription?.Speaker is not VoiSonaTalkVoiceSpeaker speaker)
                return;

            comboBox.ItemsSource = VoiSonaTalkAPIHelper.GetVersionsByVoiceName(speaker.ID);
            comboBox.SetBinding(CommonComboBox.ValueProperty, ItemPropertiesBinding.Create2(itemProperties));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not CommonComboBox comboBox)
                return;
            BindingOperations.ClearBinding(comboBox, CommonComboBox.ValueProperty);
            comboBox.ItemsSource = null;
        }
    }
}
