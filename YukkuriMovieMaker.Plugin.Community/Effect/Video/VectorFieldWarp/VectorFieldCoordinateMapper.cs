using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal static class VectorFieldCoordinateMapper
    {
        public static Point ItemToImage(Point item, Rect imageBounds, double imageScale)
            => new(
                (item.X - imageBounds.Left) * imageScale,
                (item.Y - imageBounds.Top) * imageScale);

        public static Point ImageToItem(Point image, Rect imageBounds, double imageScale)
            => new(
                image.X / imageScale + imageBounds.Left,
                image.Y / imageScale + imageBounds.Top);

    }
}
