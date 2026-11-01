using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    readonly record struct PenOrderBadge(int LayerIndex, int StrokeIndex, Point Start, int Number, bool IsIndependent, bool IsDrawn, bool IsSelected, bool IsMovable);
}
