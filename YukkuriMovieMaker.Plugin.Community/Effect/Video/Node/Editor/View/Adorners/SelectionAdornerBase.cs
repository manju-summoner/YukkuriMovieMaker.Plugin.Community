using System.Windows;
using System.Windows.Documents;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View.Adorners;

public abstract class SelectionAdornerBase : Adorner
{
    protected SelectionAdornerBase(UIElement adornedElement) : base(adornedElement)
    {
    }

    public abstract void Clear();
}