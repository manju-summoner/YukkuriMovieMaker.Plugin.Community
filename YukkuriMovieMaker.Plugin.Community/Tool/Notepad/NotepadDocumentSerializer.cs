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

        public static void Save(string filePath, string text, NotepadImageStore store)
        {
                var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, PackageExtension, StringComparison.OrdinalIgnoreCase))
                SavePackage(filePath, text, store);
            else
                File.WriteAllText(filePath, text, new UTF8Encoding(false));
        }

        public static (string Text, IReadOnlyList<string> FailedImageIds) Load(string filePath, NotepadImageStore store)
        {
            var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, PackageExtension, StringComparison.OrdinalIgnoreCase))
                return LoadPackage(filePath, store);

            var bytes = File.ReadAllBytes(filePath);
            var encoding = EncodingChecker.GetAvailableEncodings(bytes).FirstOrDefault() ?? Encoding.UTF8;
            return (encoding.GetString(bytes), []);
        }

        private static void SavePackage(string filePath, string text, NotepadImageStore store)
        {
            var resolvedImages = ResolveImageReferences(text, store);

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
                        entryStream.Write(reference.Data);
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

        private static IReadOnlyList<(string Id, NotepadImageReference Reference)> ResolveImageReferences(string text, NotepadImageStore store)
        {
            var resolved = new List<(string, NotepadImageReference)>();
            var missing = new List<string>();
            foreach (var id in NotepadImagePlaceholder.CollectImageIds(text))
            {
                if (!store.TryGet(id, out var reference))
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

        private static (string Text, IReadOnlyList<string> FailedImageIds) LoadPackage(string filePath, NotepadImageStore store)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);

            string? text = null;
            var failedIds = new List<string>();
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
                if (!NotepadImageFormat.IsValidImageId(id))
                    continue;
                if (!NotepadImageFormat.TryNormalizeExtension(Path.GetExtension(entry.Name), out var extension))
                    continue;
                if (entry.Length > MaxImageEntryBytes)
                {
                    Log.Default.Write($"Notepad image entry exceeds the maximum allowed size: {entry.FullName}");
                    failedIds.Add(id);
                    continue;
                }

                var bytes = ReadEntryBytes(entry);
                try
                {
                    store.RegisterWithId(id, bytes, extension);
                }
                catch (NotSupportedException ex)
                {
                    Log.Default.Write($"Failed to load notepad image entry: {entry.FullName}", ex);
                    failedIds.Add(id);
                }
            }

            var resultText = RemoveFailedPlaceholders(text ?? string.Empty, failedIds);
            return (resultText, failedIds);
        }

        private static string RemoveFailedPlaceholders(string text, IReadOnlyList<string> failedIds)
        {
            if (failedIds.Count == 0 || text.Length == 0)
                return text;
            var result = text;
            foreach (var id in failedIds)
                result = result.Replace(NotepadImageFormat.BuildPlaceholder(id), string.Empty);
            return result;
        }

        private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
        {
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            return ms.ToArray();
        }

        private static bool IsDirectImageEntry(string fullName)
        {
            var remainder = fullName[ImagesDirectoryName.Length..];
            return !remainder.Contains('/') && !remainder.Contains('\\');
        }
    }
}
