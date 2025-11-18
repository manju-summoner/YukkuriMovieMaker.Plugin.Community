using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorAttribute : PropertyEditorAttribute
    {

        public override FrameworkElement Create()
        {
            return new VoiSonaTalkEditor();
        }

        public override void SetBindings(FrameworkElement control, object item, object propertyOwner, PropertyInfo propertyInfo)
        {
            if(control is not VoiSonaTalkEditor editor)
                return;
            if(propertyOwner is not VoiSonaTalkVoicePronounce pronounce)
                return;
            editor.Pronounce = pronounce;
        }
        public override void ClearBindings(FrameworkElement control)
        {
            if(control is not VoiSonaTalkEditor editor)
                return;
            editor.Pronounce = null;
        }
    }
}
