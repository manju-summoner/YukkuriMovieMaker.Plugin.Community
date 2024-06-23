using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.FileSource;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.HeightmapFile
{
    internal class HeightmapFileSource : IVideoEffectProcessor
    {
        readonly DisposeCollector disposer = new();
        readonly IGraphicsDevicesAndContext devices;
        readonly HeightmapFileParameter heightmapFileParameter;

        public ID2D1Image Output { get; }

        readonly Flood flat;
        IVideoFileSource? video;
        IImageFileSource? image;
        readonly AffineTransform2D transform;

        bool isFirst = true;
        string? file;
        double x, y, zoom, rotation;
        Vector2 offset;

        public HeightmapFileSource(IGraphicsDevicesAndContext devices, HeightmapFileParameter heightmapFileParameter)
        {
            this.devices = devices;
            this.heightmapFileParameter = heightmapFileParameter;

            flat = new(devices.DeviceContext) { Color = new System.Numerics.Vector4(1f,1f,1f,1f) };
            disposer.Collect(flat);

            transform = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(transform);

            Output = transform.Output;
            disposer.Collect(Output);
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var file = heightmapFileParameter.File;
            var x = heightmapFileParameter.X.GetValue(frame, length, fps);
            var y = heightmapFileParameter.Y.GetValue(frame, length, fps);
            var zoom = heightmapFileParameter.Zoom.GetValue(frame, length, fps);
            var rotation = heightmapFileParameter.Rotation.GetValue(frame, length, fps);
            var offset = this.offset;


            if(isFirst || this.file != file)
            {
                if (video != null)
                    disposer.RemoveAndDispose(ref video);
                if (image != null)
                    disposer.RemoveAndDispose(ref image);

                if (!string.IsNullOrEmpty(file))
                {
                    video = VideoFileSourceFactory.Create(devices, file);
                    if (video != null)
                    {
                        disposer.Collect(video);
                        transform.SetInput(0, video.Output, true);
                        offset = new();
                    }
                    else
                    {
                        image = ImageFileSourceFactory.Create(devices, file);
                        if (image != null)
                        {
                            disposer.Collect(image);
                            transform.SetInput(0, image.Output, true);
                            offset = new(-image.Output.PixelSize.Width / 2, -image.Output.PixelSize.Height / 2);
                        }
                        else
                        {
                            using var image = flat.Output;
                            transform.SetInput(0, image, true);
                            offset = new();
                        }
                    }
                }
                else
                {
                    using var image = flat.Output;
                    transform.SetInput(0, image, true);
                }
            }
            if (isFirst || this.x != x || this.y != y || this.zoom != zoom || this.rotation != rotation || this.offset != offset)
                transform.TransformMatrix =
                    Matrix3x2.CreateTranslation(offset)
                    * Matrix3x2.CreateScale((float)zoom / 100)
                    * Matrix3x2.CreateRotation((float)rotation / 180 * MathF.PI)
                    * Matrix3x2.CreateTranslation((float)x, (float)y);

            video?.Update(effectDescription.ItemPosition.Time);

            isFirst = false;
            this.file = file;
            this.x = x;
            this.y = y;
            this.zoom = zoom;
            this.rotation = rotation;
            this.offset = offset;

            return effectDescription.DrawDescription;


        }
        public void Dispose()
        {
            transform.SetInput(0, null, true);
            disposer.Dispose();
        }

        public void SetInput(ID2D1Image? input)
        {

        }

        public void ClearInput()
        {

        }
    }
}