namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class AviUtlScriptContext
    {
        public int ImageWidth  { get; init; }
        public int ImageHeight { get; init; }

        public double X      { get; set; }
        public double Y      { get; set; }
        public double Z      { get; set; }
        public double Ox     { get; set; }
        public double Oy     { get; set; }
        public double Zoom   { get; set; }
        public double Aspect { get; set; }
        public double Alpha  { get; set; }
        public double Rx     { get; set; }
        public double Ry     { get; set; }
        public double Rz     { get; set; }

        public double Track0 { get; init; }
        public double Track1 { get; init; }
        public double Track2 { get; init; }
        public double Track3 { get; init; }

        public double Time          { get; init; }
        public int    Frame         { get; init; }
        public int    TotalFrame    { get; init; }
        public int    Framerate     { get; init; }
        public int    TimelineFrame { get; init; }
        public double TimelineTime  { get; init; }
        public int    SceneWidth    { get; init; }
        public int    SceneHeight   { get; init; }
        public int    Layer         { get; init; }

        private Func<byte[]>? _pixelLoader;
        private byte[]?       _pixelBuffer;
        private bool          _pixelBufferLoaded;
        private bool          _isPixelsDirty;

        public bool IsPixelsDirty  => _isPixelsDirty;
        public bool IsPixelsLoaded => _pixelBufferLoaded;

        internal void SetPixelLoader(Func<byte[]> loader)
        {
            _pixelLoader       = loader;
            _pixelBufferLoaded = false;
            _isPixelsDirty     = false;
        }

        internal void EnsurePixelBuffer()
        {
            if (_pixelBufferLoaded) return;
            _pixelBuffer       = _pixelLoader?.Invoke();
            _pixelBufferLoaded = true;
        }

        internal byte[]? GetPixelBuffer() => _pixelBuffer;

        internal void MarkPixelsDirty() => _isPixelsDirty = true;

        public (double r, double g, double b, double a) GetPixel(int x, int y)
        {
            EnsurePixelBuffer();
            if (_pixelBuffer is null || (uint)x >= (uint)ImageWidth || (uint)y >= (uint)ImageHeight)
                return (0d, 0d, 0d, 0d);

            int    idx   = (y * ImageWidth + x) * 4;
            double a     = _pixelBuffer[idx + 3];
            if (a <= 0d) return (0d, 0d, 0d, 0d);

            double scale = 255d / a;
            return (
                Math.Clamp(_pixelBuffer[idx + 2] * scale, 0d, 255d),
                Math.Clamp(_pixelBuffer[idx + 1] * scale, 0d, 255d),
                Math.Clamp(_pixelBuffer[idx + 0] * scale, 0d, 255d),
                a
            );
        }

        public void SetPixel(int x, int y, double r, double g, double b, double a = 255d)
        {
            EnsurePixelBuffer();
            if (_pixelBuffer is null || (uint)x >= (uint)ImageWidth || (uint)y >= (uint)ImageHeight)
                return;

            _isPixelsDirty = true;
            int    idx = (y * ImageWidth + x) * 4;
            double aK  = Math.Clamp(a, 0d, 255d) / 255d;

            _pixelBuffer[idx + 0] = (byte)Math.Clamp(b * aK, 0d, 255d);
            _pixelBuffer[idx + 1] = (byte)Math.Clamp(g * aK, 0d, 255d);
            _pixelBuffer[idx + 2] = (byte)Math.Clamp(r * aK, 0d, 255d);
            _pixelBuffer[idx + 3] = (byte)Math.Clamp(a,      0d, 255d);
        }
    }
}
