using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal static class Model3DThumbnailUtil
{
    private const int DefaultSize = 128;
    private const double FieldOfView = 45.0;
    private const double CameraDistanceRatio = 2.5;
    private const double NearPlaneRatio = 0.1;
    private const double FarPlaneRatio = 5.0;
    private const byte AmbientLevel = 50;

    public static BitmapSource? CreateThumbnail(Model3DData model, int width = DefaultSize, int height = DefaultSize)
    {
        if (model.Vertices.Length == 0 || model.Indices.Length == 0) return null;

        BitmapSource? thumbnail = null;
        void Generate() => thumbnail = RenderOnStaThread(model, width, height);

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            Generate();
            return thumbnail;
        }

        var thread = new Thread(Generate);
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return thumbnail;
    }

    private static BitmapSource? RenderOnStaThread(Model3DData model, int width, int height)
    {
        try
        {
            var group = new Model3DGroup();
            group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1)));
            group.Children.Add(new AmbientLight(Color.FromRgb(AmbientLevel, AmbientLevel, AmbientLevel)));

            using var textureService = new TextureService();
            var bounds = new Rect3D();
            bool hasGeometry = false;

            foreach (var part in model.Parts)
            {
                var geometry = BuildPartGeometry(model, part);
                if (geometry is null) continue;

                group.Children.Add(new GeometryModel3D(geometry, CreateMaterial(part, textureService)));
                bounds = hasGeometry ? Rect3D.Union(bounds, geometry.Bounds) : geometry.Bounds;
                hasGeometry = true;
            }

            if (!hasGeometry) return null;

            var viewport = CreateViewport(bounds, width, height);
            viewport.Children.Add(new ModelVisual3D { Content = group });
            viewport.Measure(new Size(width, height));
            viewport.Arrange(new Rect(0, 0, width, height));

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(viewport);
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static MeshGeometry3D? BuildPartGeometry(Model3DData model, Model3DPart part)
    {
        int start = part.IndexOffset;
        int end = Math.Min(start + part.IndexCount, model.Indices.Length);
        if (start >= end) return null;

        var mesh = new MeshGeometry3D();
        var vertexMap = new Dictionary<int, int>();

        for (int i = start; i < end; i++)
        {
            int sourceIndex = model.Indices[i];
            if ((uint)sourceIndex >= (uint)model.Vertices.Length) continue;

            if (!vertexMap.TryGetValue(sourceIndex, out int mappedIndex))
            {
                mappedIndex = mesh.Positions.Count;
                vertexMap[sourceIndex] = mappedIndex;

                var vertex = model.Vertices[sourceIndex];
                mesh.Positions.Add(new Point3D(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
                mesh.Normals.Add(new Vector3D(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z));
                mesh.TextureCoordinates.Add(new Point(vertex.TexCoord.X, vertex.TexCoord.Y));
            }

            mesh.TriangleIndices.Add(mappedIndex);
        }

        return mesh.Positions.Count > 0 ? mesh : null;
    }

    private static Material CreateMaterial(Model3DPart part, ITextureService textureService)
    {
        if (!string.IsNullOrEmpty(part.TexturePath) && File.Exists(part.TexturePath))
        {
            try
            {
                var bitmap = textureService.Load(part.TexturePath);
                return new DiffuseMaterial(new ImageBrush(bitmap) { ViewportUnits = BrushMappingMode.Absolute });
            }
            catch
            {
            }
        }

        var color = part.BaseColor;
        return new DiffuseMaterial(new SolidColorBrush(Color.FromScRgb(color.W, color.X, color.Y, color.Z)));
    }

    private static Viewport3D CreateViewport(Rect3D bounds, int width, int height)
    {
        var center = new Point3D(
            bounds.X + bounds.SizeX * 0.5,
            bounds.Y + bounds.SizeY * 0.5,
            bounds.Z + bounds.SizeZ * 0.5);

        double radius = Math.Max(Math.Max(bounds.SizeX, bounds.SizeY), bounds.SizeZ) * 0.5;
        if (radius <= 0) radius = 1.0;

        double distance = radius * CameraDistanceRatio;
        var camera = new PerspectiveCamera(
            new Point3D(center.X, center.Y, center.Z + distance),
            new Vector3D(0, 0, -1),
            new Vector3D(0, 1, 0),
            FieldOfView)
        {
            NearPlaneDistance = radius * NearPlaneRatio,
            FarPlaneDistance = radius * FarPlaneRatio
        };

        return new Viewport3D
        {
            Camera = camera,
            Width = width,
            Height = height
        };
    }
}
