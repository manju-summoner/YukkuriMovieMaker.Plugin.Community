using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.FileSource;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

public static class ImageLoader
{
    public static ID2D1Image LoadImage(IGraphicsDevicesAndContext context, string filePath)
    {
        var device = context.CreateContext();
        var dc = device.DeviceContext;
        var source = ImageFileSourceFactory.Create(device, filePath) ??
                     new ImageFileSource(dc.CreateEmptyBitmap());
        var commandList = dc.CreateCommandList();

        dc.Target = commandList;
        dc.BeginDraw();
        var size = source.Output.Size;
        dc.DrawImage(source.Output, new Vector2((int)((0f - size.Width) / 2f), (int)((0f - size.Height) / 2f)));
        dc.EndDraw();
        dc.Target = null;
        commandList.Close();
        source.Dispose();
        return commandList;
    }

    public static VideoLoader? CreateVideoLoader(IGraphicsDevicesAndContext context, string filePath)
    {
        var device = context.CreateContext();

        var source = VideoFileSourceFactory.Create(device, filePath);
        var length = source?.Duration ?? new TimeSpan(0, 0, 0, 0, 0);
        var fps = (source?.GetFrameIndex(length) ?? 0.0) / length.TotalSeconds;
        return source == null ? null : new VideoLoader(source, source.GetFrameIndex(length), fps);
    }

    public class VideoLoader(IVideoFileSource source, int length, double fps) : IDisposable
    {
        public double Fps => fps;
        public int Length => length;

        public void Dispose()
        {
            source.Dispose();
        }

        public ID2D1Image LoadImage(int frame)
        {
            var time = TimeSpan.FromSeconds(frame / fps);
            source.Update(time);
            return source.Output;
        }
    }
}