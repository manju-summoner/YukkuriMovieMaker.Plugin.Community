using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Editing;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal static class NotepadClipboardHandler
    {
        private const string PngFormat = "PNG";
        private const string DibFormat = "DeviceIndependentBitmap";

        private const int BI_RGB = 0;
        private const int BI_BITFIELDS = 3;
        private const int BI_ALPHABITFIELDS = 6;

        public static bool TryHandleClipboard(TextArea textArea)
        {
            try
            {
                var dataObject = Clipboard.GetDataObject();
                if (dataObject is null)
                    return false;
                return TryHandleDataObject(textArea, dataObject);
            }
            catch
            {
                return false;
            }
        }

        public static bool ClipboardContainsHandleableImage()
        {
            try
            {
                var dataObject = Clipboard.GetDataObject();
                return dataObject is not null && CanHandleDataObject(dataObject);
            }
            catch
            {
                return false;
            }
        }

        public static bool CanHandleDataObject(IDataObject dataObject)
        {
            try
            {
                if (dataObject.GetDataPresent(PngFormat))
                    return true;
                if (dataObject.GetDataPresent(DibFormat))
                    return true;
                if (dataObject.GetDataPresent(DataFormats.Bitmap))
                    return true;
                if (dataObject.GetDataPresent(DataFormats.FileDrop) &&
                    dataObject.GetData(DataFormats.FileDrop) is string[] paths)
                {
                    foreach (var path in paths)
                    {
                        if (IsSupportedImagePath(path))
                            return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static bool TryHandleDataObject(TextArea textArea, IDataObject dataObject)
        {
            try
            {
                if (TryRegisterFromImageFiles(dataObject, out var references))
                {
                    foreach (var reference in references)
                        InsertPlaceholder(textArea, reference.Id);
                    return true;
                }

                var bitmapReference = TryRegisterFromBitmapFormats(dataObject);
                if (bitmapReference is not null)
                {
                    InsertPlaceholder(textArea, bitmapReference.Id);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        public static void InsertImageFromFile(TextArea textArea, string filePath)
        {
            var reference = NotepadImageCache.RegisterFromFile(filePath);
            InsertPlaceholder(textArea, reference.Id);
        }

        public static bool IsSupportedImagePath(string path) =>
            !string.IsNullOrEmpty(path) &&
            NotepadImageCache.TryNormalizeExtension(Path.GetExtension(path), out _);

        private static bool TryRegisterFromImageFiles(IDataObject dataObject, out IReadOnlyList<NotepadImageReference> references)
        {
            if (dataObject.GetDataPresent(DataFormats.FileDrop) &&
                dataObject.GetData(DataFormats.FileDrop) is string[] paths)
            {
                var registered = new List<NotepadImageReference>();
                foreach (var path in paths)
                {
                    if (!IsSupportedImagePath(path) || !File.Exists(path))
                        continue;
                    registered.Add(NotepadImageCache.RegisterFromFile(path));
                }
                if (registered.Count > 0)
                {
                    references = registered;
                    return true;
                }
            }
            references = [];
            return false;
        }

        private static NotepadImageReference? TryRegisterFromBitmapFormats(IDataObject dataObject)
        {
            if (TryReadStream(dataObject, PngFormat, out var pngBytes))
                return NotepadImageCache.RegisterFromBytes(pngBytes, ".png");

            if (dataObject.GetDataPresent(DataFormats.Bitmap) &&
                dataObject.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                return NotepadImageCache.RegisterFromBitmap(bitmap);

            if (TryReadStream(dataObject, DibFormat, out var dibBytes))
            {
                var pngFromDib = ConvertDibToPng(dibBytes);
                if (pngFromDib is not null)
                    return NotepadImageCache.RegisterFromBytes(pngFromDib, ".png");
            }

            return null;
        }

        private static bool TryReadStream(IDataObject dataObject, string format, out byte[] bytes)
        {
            if (dataObject.GetDataPresent(format) &&
                dataObject.GetData(format) is Stream stream)
            {
                using (stream)
                {
                    bytes = ReadStreamToEnd(stream);
                    return bytes.Length > 0;
                }
            }
            bytes = [];
            return false;
        }

        private static byte[] ReadStreamToEnd(Stream stream)
        {
            if (stream is MemoryStream memoryStream)
                return memoryStream.ToArray();

            long? originalPosition = null;
            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }
            try
            {
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
            finally
            {
                if (originalPosition.HasValue)
                    stream.Position = originalPosition.Value;
            }
        }

        private static byte[]? ConvertDibToPng(byte[] dibBytes)
        {
            try
            {
                var bmpBytes = WrapDibAsBmp(dibBytes);
                if (bmpBytes is null)
                    return null;
                using var source = new MemoryStream(bmpBytes);
                var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0)
                    return null;
                var frame = decoder.Frames[0];
                using var target = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(frame));
                encoder.Save(target);
                return target.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? WrapDibAsBmp(byte[] dib)
        {
            const int FileHeaderSize = 14;
            if (dib.Length < 4)
                return null;
            var headerSize = BitConverter.ToInt32(dib, 0);
            if (headerSize < 12 || headerSize > dib.Length)
                return null;

            int bitCount = headerSize >= 16 ? BitConverter.ToInt16(dib, 14) : 0;
            int compression = headerSize >= 20 ? BitConverter.ToInt32(dib, 16) : BI_RGB;
            int colorsUsed = headerSize >= 36 ? BitConverter.ToInt32(dib, 32) : 0;
            int paletteEntries = bitCount switch
            {
                1 or 4 or 8 => colorsUsed > 0 ? colorsUsed : 1 << bitCount,
                _ => colorsUsed,
            };
            int paletteSize = paletteEntries * 4;

            int bitfieldsSize = headerSize == 40 ? compression switch
            {
                BI_BITFIELDS => 12,
                BI_ALPHABITFIELDS => 16,
                _ => 0,
            } : 0;

            int pixelOffset = FileHeaderSize + headerSize + paletteSize + bitfieldsSize;
            int totalSize = FileHeaderSize + dib.Length;

            var result = new byte[totalSize];
            result[0] = (byte)'B';
            result[1] = (byte)'M';
            BitConverter.GetBytes(totalSize).CopyTo(result, 2);
            BitConverter.GetBytes(pixelOffset).CopyTo(result, 10);
            Buffer.BlockCopy(dib, 0, result, FileHeaderSize, dib.Length);
            return result;
        }

        private static void InsertPlaceholder(TextArea textArea, string id)
        {
            var placeholder = NotepadImageCache.BuildPlaceholder(id);
            var document = textArea.Document;
            if (document is null)
                return;

            using (document.RunUpdate())
            {
                var selection = textArea.Selection;
                if (!selection.IsEmpty)
                    selection.ReplaceSelectionWithText(placeholder);
                else
                    document.Insert(textArea.Caret.Offset, placeholder);
            }
            textArea.Caret.BringCaretToView();
        }
    }
}
