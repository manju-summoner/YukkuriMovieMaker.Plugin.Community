namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    internal sealed class PenOrderBadgeEventArgs(int layerIndex, int strokeIndex, bool isToggle) : EventArgs
    {
        public int LayerIndex { get; } = layerIndex;

        public int StrokeIndex { get; } = strokeIndex;

        public bool IsToggle { get; } = isToggle;
    }
}
