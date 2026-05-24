using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal class ImageDownloaderItemViewModel : Bindable
    {
        static readonly HttpClient httpClient = new();

        readonly ImageSource source;

        public string Url { get; }
        public string FileName => source.FileName;
        public bool IsSelected { get => field; set => Set(ref field, value); } = true;
        public BitmapImage? Thumbnail { get => field; private set => Set(ref field, value); }

        public ImageDownloaderItemViewModel(string url)
        {
            Url = url;
            source = ImageSource.From(url);
        }

        public async Task<bool> TryLoadThumbnailAsync(CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await source.GetBytesAsync(cancellationToken);
                if (bytes.Length == 0)
                    return false;

                var image = new BitmapImage();
                using var stream = new MemoryStream(bytes);
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 120;
                image.EndInit();
                image.Freeze();
                Thumbnail = image;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SaveToAsync(string folder, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await source.GetBytesAsync(cancellationToken);
                if (bytes.Length == 0)
                    return false;

                var fileName = SanitizeFileName(FileName);
                var destPath = GetUniqueFilePath(folder, fileName);
                await File.WriteAllBytesAsync(destPath, bytes, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        }

        static string GetUniqueFilePath(string folder, string fileName)
        {
            var path = Path.Combine(folder, fileName);
            if (!File.Exists(path))
                return path;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var index = 1;
            do
            {
                path = Path.Combine(folder, $"{nameWithoutExt}_{index}{ext}");
                index++;
            } while (File.Exists(path));
            return path;
        }

        abstract class ImageSource
        {
            public abstract string FileName { get; }
            public abstract Task<byte[]> GetBytesAsync(CancellationToken cancellationToken);

            public static ImageSource From(string url) =>
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    ? new DataUriImageSource(url)
                    : new HttpImageSource(url);
        }

        sealed class HttpImageSource(string url) : ImageSource
        {
            public override string FileName =>
                Path.GetFileName(new Uri(url).AbsolutePath) is { Length: > 0 } name ? name : "image";

            public override Task<byte[]> GetBytesAsync(CancellationToken cancellationToken) =>
                httpClient.GetByteArrayAsync(url, cancellationToken);
        }

        sealed class DataUriImageSource(string dataUri) : ImageSource
        {
            static readonly Dictionary<string, string> mimeToExtension = new(StringComparer.OrdinalIgnoreCase)
            {
                ["image/png"] = ".png",
                ["image/jpeg"] = ".jpg",
                ["image/gif"] = ".gif",
                ["image/webp"] = ".webp",
                ["image/bmp"] = ".bmp",
                ["image/svg+xml"] = ".svg",
                ["image/avif"] = ".avif",
                ["image/tiff"] = ".tiff",
                ["image/ico"] = ".ico",
                ["image/x-icon"] = ".ico",
            };

            public override string FileName
            {
                get
                {
                    var mime = ParseMimeType(dataUri);
                    var ext = mimeToExtension.TryGetValue(mime, out var e) ? e : ".bin";
                    return "image" + ext;
                }
            }

            public override Task<byte[]> GetBytesAsync(CancellationToken cancellationToken)
            {
                var commaIndex = dataUri.IndexOf(',');
                if (commaIndex < 0)
                    return Task.FromResult(Array.Empty<byte>());

                var header = dataUri[..commaIndex];
                var data = dataUri[(commaIndex + 1)..];

                var bytes = header.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                    ? Convert.FromBase64String(data)
                    : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data));

                return Task.FromResult(bytes);
            }

            static string ParseMimeType(string dataUri)
            {
                var colon = dataUri.IndexOf(':');
                var semicolonOrComma = dataUri.IndexOfAny([';', ','], colon + 1);
                if (colon < 0 || semicolonOrComma < 0)
                    return string.Empty;
                return dataUri[(colon + 1)..semicolonOrComma];
            }
        }
    }
}
