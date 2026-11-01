namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    class PenDrawResources : IDisposable
    {
        public InkStyleResourceManager InkStyles { get; } = new();

        public SolidColorBrushManager SolidBrushes { get; } = new();

        public PencilBrushManager PencilBrushes { get; } = new();

        public void BeginUse()
        {
            InkStyles.BeginUse();
            SolidBrushes.BeginUse();
            PencilBrushes.BeginUse();
        }

        public void EndUse()
        {
            InkStyles.EndUse();
            SolidBrushes.EndUse();
            PencilBrushes.EndUse();
        }

        public void Dispose()
        {
            InkStyles.Dispose();
            SolidBrushes.Dispose();
            PencilBrushes.Dispose();
        }
    }
}
