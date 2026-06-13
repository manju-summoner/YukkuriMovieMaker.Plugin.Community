namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal static class NotepadImageFormat
    {
        public const string PlaceholderPrefix = "\ufffc[image:";
        public const string PlaceholderSuffix = "]";

        private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".tif"];

        public static bool IsValidImageId(string id) =>
            !string.IsNullOrEmpty(id) &&
            id.Length <= 128 &&
            id.All(static c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        public static string GetSupportedImageFileFilter() =>
            string.Join(';', AllowedExtensions.Select(static ext => $"*{ext}"));

        public static bool TryNormalizeExtension(string extension, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(extension))
                return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith('.'))
                ext = "." + ext;
            if (!AllowedExtensions.Contains(ext))
                return false;
            normalized = ext;
            return true;
        }

        public static string BuildPlaceholder(string id) => $"{PlaceholderPrefix}{id}{PlaceholderSuffix}";
    }
}
