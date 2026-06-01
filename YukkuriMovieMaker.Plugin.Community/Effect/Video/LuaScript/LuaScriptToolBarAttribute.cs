using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaScriptToolBarAttribute : PropertyEditorAttribute2
    {
        public override PropertyEditorSize PropertyEditorSize { get; set; } = PropertyEditorSize.FullWidth;

        public override FrameworkElement Create() => new LuaScriptToolBar();

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is LuaScriptToolBar editor)
                editor.ItemProperties = null;
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is LuaScriptToolBar editor)
                editor.ItemProperties = itemProperties;
        }
    }
}
