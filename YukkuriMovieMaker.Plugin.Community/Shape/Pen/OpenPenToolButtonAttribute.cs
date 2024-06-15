using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class OpenPenToolButtonAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new OpenPenToolButton();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not OpenPenToolButton editor)
                return;

            editor.ItemProperties = itemProperties;
        }
        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not OpenPenToolButton editor)
                return;
            editor.ItemProperties = null;
        }
    }
}
