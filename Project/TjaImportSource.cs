using System;
using System.IO;
using System.Text;
using TaikoSoundEditor.Commons.IO;

namespace TaikoSoundEditor.Project
{
    internal sealed class TjaImportSource
    {
        private TjaImportSource(string path, TJA tja, string[] lines, string encodingName)
        {
            Path = path;
            Tja = tja;
            Lines = lines;
            EncodingName = encodingName;
        }

        public string Path { get; }
        public TJA Tja { get; }
        public string[] Lines { get; }
        public string EncodingName { get; }

        public static TjaImportSource LoadAuto(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A TJA path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("The TJA file was not found.", path);

            var bytes = File.ReadAllBytes(path);
            if (HasUtf8Bom(bytes))
                return FromBytes(path, bytes, new UTF8Encoding(true, true), "UTF-8 BOM");

            try
            {
                // A large amount of ESE content is UTF-8 without a BOM. Decode strictly
                // first so invalid UTF-8 falls back to Shift-JIS instead of becoming �.
                return FromBytes(path, bytes, new UTF8Encoding(false, true), "UTF-8");
            }
            catch (DecoderFallbackException)
            {
                return FromBytes(path, bytes, Encoding.GetEncoding("shift_jis"), "Shift-JIS");
            }
        }

        public static TjaImportSource LoadUtf8(string path) =>
            FromBytes(path, File.ReadAllBytes(path), new UTF8Encoding(false, true), "UTF-8");

        public static TjaImportSource LoadShiftJis(string path) =>
            FromBytes(path, File.ReadAllBytes(path), Encoding.GetEncoding("shift_jis"), "Shift-JIS");

        private static TjaImportSource FromBytes(string path, byte[] bytes, Encoding encoding,
            string encodingName)
        {
            var text = encoding.GetString(bytes ?? Array.Empty<byte>());
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return new TjaImportSource(path, new TJA(lines), lines, encodingName);
        }

        private static bool HasUtf8Bom(byte[] bytes) =>
            bytes != null && bytes.Length >= 3 &&
            bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }
}
