using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public class ConnectionAdorner : Adorner
{
    public ConnectionAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (DataContext is not GraphViewModel vm)
            return;

        if (vm is { DraggingFromPort: not null, TemporaryEndPoint: not null })
        {
            var start = vm.TransformToScreen(vm.DraggingFromPort.Position);
            var end = vm.TemporaryEndPoint.Value;
            dc.DrawLine(new Pen(Brushes.Orange, 2), start, end);
        }
    }
}