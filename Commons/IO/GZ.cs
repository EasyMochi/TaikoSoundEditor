using System.IO;
using System.IO.Compression;
using System.Text;
using TaikoSoundEditor.Commons.Utils;

namespace TaikoSoundEditor.Commons.IO
{
    internal static class GZ
    {
        public static string DecompressBytes(byte[] bytes)
        {
            Logger.Info("GZ Decompressing bytes");
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }

        public static string DecompressString(string gzPath)
        {
            Logger.Info("GZ Decompressing string");
            using var input = File.OpenRead(gzPath);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }

        public static byte[] DecompressBytes(string gzPath)
        {
            Logger.Info("GZ Decompressing bytes");
            using var input = File.OpenRead(gzPath);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        public static byte[] CompressToBytes(string content) =>
            CompressToBytes(Encoding.UTF8.GetBytes(content));

        public static byte[] CompressToBytes(byte[] uncompressed)
        {
            Logger.Info("GZ Compressing bytes");
            if (uncompressed == null) throw new System.ArgumentNullException(nameof(uncompressed));

            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(uncompressed, 0, uncompressed.Length);
            return output.ToArray();
        }
    }
}
