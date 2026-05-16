using System.IO;
using System.IO.Compression;
using System.Text;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal static class NotepadDocumentSerializer
    {
        public const string PackageExtension = ".ymmnote";
        public const string PlainTextExtension = ".txt";
        private const string ContentEntryName = "content.txt";
        private const string ImagesDirectoryName = "images/";
        private const long MaxImageEntryBytes = 256L * 1024 * 1024;
        private const long MaxContentEntryBytes = 256L * 1024 * 1024;

        public static bool ContainsImages(string text) =>
            NotepadImagePlaceholder.Pattern.IsMatch(text ?? string.Empty);

        public static string DetermineSaveExtension(string text) =>
            ContainsImages(text) ? PackageExtension : PlainTextExtension;

        public static void Save(string filePath, string text)
        {
            var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, PackageExtension, StringComparison.OrdinalIgnoreCase))
                SavePackage(filePath, text);
            else
                File.WriteAllText(filePath, text, new UTF8Encoding(false));
        }

        public static (string Text, string ResolvedExtension) Load(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, PackageExtension, StringComparison.OrdinalIgnoreCase))
                return (LoadPackage(filePath), PackageExtension);

            var bytes = File.ReadAllBytes(filePath);
            var encoding = EncodingChecker.GetAvailableEncodings(bytes).FirstOrDefault() ?? Encoding.UTF8;
            return (encoding.GetString(bytes), PlainTextExtension);
        }

        private static void SavePackage(string filePath, string text)
        {
            var resolvedImages = ResolveImageReferences(text);

            var tempPath = filePath + ".tmp";
            try
            {
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
                {
                    var contentEntry = archive.CreateEntry(ContentEntryName, CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(contentEntry.Open(), new UTF8Encoding(false)))
                        writer.Write(text);

                    foreach (var (id, reference) in resolvedImages)
                    {
                        var entryName = $"{ImagesDirectoryName}{id}{reference.Extension}";
                        var imageEntry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                        using var entryStream = imageEntry.Open();
                        using var source = File.OpenRead(reference.CachePath);
                        source.CopyTo(entryStream);
                    }
                }

                File.Move(tempPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        private static IReadOnlyList<(string Id, NotepadImageReference Reference)> ResolveImageReferences(string text)
        {
            var resolved = new List<(string, NotepadImageReference)>();
            var missing = new List<string>();
            foreach (var id in NotepadImagePlaceholder.CollectImageIds(text))
            {
                if (!NotepadImageCache.TryGet(id, out var reference) || !File.Exists(reference.CachePath))
                {
                    missing.Add(id);
                    continue;
                }
                resolved.Add((id, reference));
            }
            if (missing.Count > 0)
                throw new InvalidOperationException($"{Texts.MissingImageData} ({string.Join(", ", missing)})");
            return resolved;
        }

        private static string LoadPackage(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);

            string? text = null;
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, ContentEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.Length > MaxContentEntryBytes)
                        throw new InvalidOperationException(Texts.ContentEntryTooLarge);
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    text = reader.ReadToEnd();
                    continue;
                }

                if (!entry.FullName.StartsWith(ImagesDirectoryName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                if (!IsDirectImageEntry(entry.FullName))
                    continue;

                var id = Path.GetFileNameWithoutExtension(entry.Name).ToLowerInvariant();
                if (!NotepadImageCache.IsValidImageId(id))
                    continue;
                if (!NotepadImageCache.TryNormalizeExtension(Path.GetExtension(entry.Name), out var extension))
                    continue;

                var destination = Path.Combine(NotepadImageCache.CacheDirectory, $"{id}{extension}");
                if (!IsWithinCacheDirectory(destination))
                    continue;
                if (entry.Length > MaxImageEntryBytes)
                    continue;

                ExtractEntryAtomically(entry, destination);
                NotepadImageCache.RegisterExistingCacheFile(destination);
            }

            return text ?? string.Empty;
        }

        private static void ExtractEntryAtomically(ZipArchiveEntry entry, string destination)
        {
            var tempPath = Path.Combine(NotepadImageCache.CacheDirectory, $"{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var entryStream = entry.Open())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    entryStream.CopyTo(fileStream);
                File.Move(tempPath, destination, overwrite: true);
            }
            catch (IOException) when (File.Exists(destination))
            {
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static bool IsDirectImageEntry(string fullName)
        {
            var remainder = fullName[ImagesDirectoryName.Length..];
            return !remainder.Contains('/') && !remainder.Contains('\\');
        }

        private static bool IsWithinCacheDirectory(string destination)
        {
            var cacheDirectory = Path.GetFullPath(NotepadImageCache.CacheDirectory);
            var fullDestination = Path.GetFullPath(destination);
            var separator = Path.DirectorySeparatorChar;
            var prefix = cacheDirectory.EndsWith(separator) ? cacheDirectory : cacheDirectory + separator;
            return fullDestination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
