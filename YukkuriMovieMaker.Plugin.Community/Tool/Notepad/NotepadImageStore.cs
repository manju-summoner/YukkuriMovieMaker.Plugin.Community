using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadImageStore
    {
        private sealed class StoredImage
        {
            public required NotepadImageReference Reference { get; init; }
            public BitmapSource? Bitmap { get; set; }
        }

        private readonly ConcurrentDictionary<string, StoredImage> images = new();

        public NotepadImageReference RegisterFromBytes(byte[] data, string extension)
        {
            var id = ComputeHash(data);
            return Register(id, data, extension);
        }

        public NotepadImageReference RegisterWithId(string id, byte[] data, string extension)
        {
            if (!NotepadImageFormat.IsValidImageId(id))
                throw new ArgumentException("Invalid image id.", nameof(id));
            return Register(id, data, extension);
        }

        private NotepadImageReference Register(string id, byte[] data, string extension)
        {
            if (images.TryGetValue(id, out var existing))
                return existing.Reference;

            var codecExtension = DecodeAndValidate(data);
            var fallbackExt = NotepadImageFormat.TryNormalizeExtension(extension, out var normalized) ? normalized : ".png";
            var finalExt = codecExtension ?? fallbackExt;

            var reference = new NotepadImageReference(id, data, finalExt);
            images[id] = new StoredImage { Reference = reference };
            return reference;
        }

        public NotepadImageReference RegisterFromFile(string sourcePath)
        {
            var bytes = File.ReadAllBytes(sourcePath);
            var ext = Path.GetExtension(sourcePath);
            return RegisterFromBytes(bytes, ext);
        }

        public NotepadImageReference RegisterFromBitmap(BitmapSource bitmap)
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return RegisterFromBytes(ms.ToArray(), ".png");
        }

        public bool TryGet(string id, out NotepadImageReference reference)
        {
            if (images.TryGetValue(id, out var stored))
            {
                reference = stored.Reference;
                return true;
            }
            reference = null!;
            return false;
        }

        public BitmapSource? GetBitmap(string id)
        {
            if (!images.TryGetValue(id, out var stored))
                return null;
            if (stored.Bitmap is not null)
                return stored.Bitmap;
            try
            {
                using var stream = new MemoryStream(stored.Reference.Data, writable: false);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                stored.Bitmap = bitmap;
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static string? DecodeAndValidate(byte[] data)
        {
            try
            {
                using var stream = new MemoryStream(data, writable: false);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0)
                    throw new NotSupportedException(Texts.UnsupportedImageFormat);
                var frame = decoder.Frames[0];
                if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
                    throw new NotSupportedException(Texts.UnsupportedImageFormat);
                return DeriveExtensionFromCodec(decoder.CodecInfo);
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(Texts.UnsupportedImageFormat, ex);
            }
        }

        private static string? DeriveExtensionFromCodec(BitmapCodecInfo? codecInfo)
        {
            if (codecInfo is null)
                return null;
            var extensions = codecInfo.FileExtensions;
            if (string.IsNullOrEmpty(extensions))
                return null;
            foreach (var candidate in extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (NotepadImageFormat.TryNormalizeExtension(candidate, out var normalized))
                    return normalized;
            }
            return null;
        }

        private static string ComputeHash(byte[] data)
        {
            var hash = SHA256.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
