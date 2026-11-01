using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Attributes
{
    internal class PenLayerSliderAttribute(string stringFormat, string unitText, double min, double max, string enabledPath)
        : TextBoxSliderAttribute(stringFormat, unitText, min, max)
    {
        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            base.SetBindings(control, itemProperties);
            PenLayerEditorBinding.SetEnabled(control, itemProperties, enabledPath);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            PenLayerEditorBinding.ClearEnabled(control);
            base.ClearBindings(control);
        }
    }
}
