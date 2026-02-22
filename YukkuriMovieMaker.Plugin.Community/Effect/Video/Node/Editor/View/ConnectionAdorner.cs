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

        var pen = new Pen(Brushes.LightGray, 2);

        foreach (var conn in vm.Connections)
        {
            if (conn.FromPort?.Position == null ||
                conn.ToPort?.Position == null)
                continue;

            var p1 = conn.FromPort.Position;
            var p2 = conn.ToPort.Position;

            dc.DrawLine(pen, p1, p2);
        }
    }
}