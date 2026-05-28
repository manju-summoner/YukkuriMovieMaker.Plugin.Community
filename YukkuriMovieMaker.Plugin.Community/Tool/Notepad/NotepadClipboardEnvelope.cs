using System.IO;
using System.Text;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal static class NotepadClipboardEnvelope
    {
        public const string DataFormat = "YukkuriMovieMaker.Plugin.Community.Tool.Notepad.Images.v1";

        private const int Magic = 0x4E4D4D59;
        private const int Version = 1;
        private const int MaxImageCount = 4096;
        private const int MaxImageBytes = 256 * 1024 * 1024;

        public static byte[] Serialize(IReadOnlyList<NotepadImageReference> references)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: false);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(references.Count);
            foreach (var r in references)
            {
                writer.Write(r.Id);
                writer.Write(r.Extension);
                writer.Write(r.Data.Length);
                writer.Write(r.Data);
            }
            return ms.ToArray();
        }

        public static bool TryDeserialize(byte[] bytes, out List<(string Id, byte[] Data, string Extension)> result)
        {
            result = [];
            if (bytes is null || bytes.Length < 12)
                return false;
            try
            {
                using var ms = new MemoryStream(bytes, writable: false);
                using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: false);
                if (reader.ReadInt32() != Magic)
                    return false;
                if (reader.ReadInt32() != Version)
                    return false;
                var count = reader.ReadInt32();
                if (count < 0 || count > MaxImageCount)
                    return false;
                for (int i = 0; i < count; i++)
                {
                    var id = reader.ReadString();
                    var ext = reader.ReadString();
                    var len = reader.ReadInt32();
                    if (len < 0 || len > MaxImageBytes)
                        return false;
                    var data = reader.ReadBytes(len);
                    if (data.Length != len)
                        return false;
                    result.Add((id, data, ext));
                }
                return true;
            }
            catch
            {
                result = [];
                return false;
            }
        }
    }
}
