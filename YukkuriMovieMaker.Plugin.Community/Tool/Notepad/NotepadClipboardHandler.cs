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

        public static bool TryHandleClipboard(TextArea textArea, NotepadImageStore store)
        {
            try
            {
                var dataObject = Clipboard.GetDataObject();
                if (dataObject is null)
                    return false;
                return TryHandleDataObject(textArea, dataObject, store);
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

        public static bool TryHandleDataObject(TextArea textArea, IDataObject dataObject, NotepadImageStore store)
        {
            try
            {
                // envelopeで画像をImageStoreへ登録。
                // 成功した場合はfalseを返し、AvalonEdit側の貼り付け処理でプレースホルダー文字列を含む本文テキストを扱う。
                if (TryImportEnvelope(dataObject, store))
                    return false;

                if (TryRegisterFromImageFiles(dataObject, store, out var references))
                {
                    foreach (var reference in references)
                        InsertPlaceholder(textArea, reference.Id);
                    return true;
                }

                var bitmapReference = TryRegisterFromBitmapFormats(dataObject, store);
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

        public static void AttachImageEnvelope(IDataObject dataObject, string copiedText, NotepadImageStore store)
        {
            if (string.IsNullOrEmpty(copiedText))
                return;
            var seen = new HashSet<string>();
            var references = new List<NotepadImageReference>();
            foreach (var id in NotepadImagePlaceholder.CollectImageIds(copiedText))
            {
                if (!seen.Add(id))
                    continue;
                if (store.TryGet(id, out var reference))
                    references.Add(reference);
            }
            if (references.Count == 0)
                return;
            try
            {
                var payload = NotepadClipboardEnvelope.Serialize(references);
                dataObject.SetData(NotepadClipboardEnvelope.DataFormat, payload);
            }
            catch
            {
            }
        }

        private static bool TryImportEnvelope(IDataObject dataObject, NotepadImageStore store)
        {
            if (!dataObject.GetDataPresent(NotepadClipboardEnvelope.DataFormat))
                return false;
            byte[]? bytes = dataObject.GetData(NotepadClipboardEnvelope.DataFormat) switch
            {
                byte[] b => b,
                MemoryStream ms => ms.ToArray(),
                _ => null,
            };
            if (bytes is null)
                return false;
            if (!NotepadClipboardEnvelope.TryDeserialize(bytes, out var images))
                return false;
            foreach (var (id, data, extension) in images)
            {
                try { store.RegisterWithId(id, data, extension); }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
            }
            return true;
        }

        public static void InsertImageFromFile(TextArea textArea, string filePath, NotepadImageStore store)
        {
            var reference = store.RegisterFromFile(filePath);
            InsertPlaceholder(textArea, reference.Id);
        }

        public static bool IsSupportedImagePath(string path) =>
            !string.IsNullOrEmpty(path) &&
            NotepadImageFormat.TryNormalizeExtension(Path.GetExtension(path), out _);

        private static bool TryRegisterFromImageFiles(IDataObject dataObject, NotepadImageStore store, out IReadOnlyList<NotepadImageReference> references)
        {
            if (dataObject.GetDataPresent(DataFormats.FileDrop) &&
                dataObject.GetData(DataFormats.FileDrop) is string[] paths)
            {
                var registered = new List<NotepadImageReference>();
                foreach (var path in paths)
                {
                    if (!IsSupportedImagePath(path) || !File.Exists(path))
                        continue;
                    registered.Add(store.RegisterFromFile(path));
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

        private static NotepadImageReference? TryRegisterFromBitmapFormats(IDataObject dataObject, NotepadImageStore store)
        {
            if (TryReadStream(dataObject, PngFormat, out var pngBytes))
                return store.RegisterFromBytes(pngBytes, ".png");

            if (dataObject.GetDataPresent(DataFormats.Bitmap) &&
                dataObject.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                return store.RegisterFromBitmap(bitmap);

            if (TryReadStream(dataObject, DibFormat, out var dibBytes))
            {
                var pngFromDib = ConvertDibToPng(dibBytes);
                if (pngFromDib is not null)
                    return store.RegisterFromBytes(pngFromDib, ".png");
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
            var placeholder = NotepadImageFormat.BuildPlaceholder(id);
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
