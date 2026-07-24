using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal sealed class EventFolderDataDocument
    {
        private readonly JsonArray items;

        private EventFolderDataDocument(JsonArray items, string sourcePath)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            SourcePath = sourcePath;
        }

        public JsonArray Items => items;
        public string SourcePath { get; }

        public static EventFolderDataDocument CreateEmpty() =>
            new EventFolderDataDocument(new JsonArray(), null);

        public static EventFolderDataDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("An event-folder data path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Event-folder data was not found.", path);

            var bytes = File.ReadAllBytes(path);
            if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                bytes = Decompress(bytes, stream => new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false));
            else if (path.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
                bytes = Decompress(bytes, stream => new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: false));

            var json = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            var root = JsonNode.Parse(json) as JsonArray
                ?? throw new InvalidDataException("event_folder_data must contain a JSON array.");
            return new EventFolderDataDocument(root, Path.GetFullPath(path));
        }

        public string ToJson(bool indented = true) => items.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = indented,
            IndentCharacter = ' ',
            IndentSize = 4
        });

        public void WriteAll(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("An output directory is required.", nameof(directory));

            ValidateForWrite();
            directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(directory);
            var json = Encoding.UTF8.GetBytes(ToJson(true));
            var jsonPath = Path.Combine(directory, "event_folder_data.json");
            var gzipPath = Path.Combine(directory, "event_folder_data.json.gz");
            var brotliPath = Path.Combine(directory, "event_folder_data.json.br");

            WriteAtomic(jsonPath, json);
            WriteAtomic(gzipPath,
                Compress(json, stream => new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true)));
            WriteAtomic(brotliPath,
                Compress(json, stream => new BrotliStream(stream, CompressionLevel.Optimal, leaveOpen: true)));

            EnsureRoundTrip(jsonPath);
            EnsureRoundTrip(gzipPath);
            EnsureRoundTrip(brotliPath);
        }

        private void ValidateForWrite()
        {
            var folderIds = new System.Collections.Generic.HashSet<int>();
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] is not JsonObject row)
                    throw new InvalidDataException($"Event-folder row {index + 1} is not a JSON object.");
                var folderId = ReadInt(row["folderId"])
                    ?? throw new InvalidDataException($"Event-folder row {index + 1} has no numeric folderId.");
                if (!folderIds.Add(folderId))
                    throw new InvalidDataException($"Event-folder data contains duplicate folderId {folderId}.");
                if (ReadInt(row["verupNo"]) == null)
                    throw new InvalidDataException($"Event folder {folderId} has no numeric verupNo.");
                if (ReadInt(row["priority"]) == null)
                    throw new InvalidDataException($"Event folder {folderId} has no numeric priority.");
                if (row["songNo"] is not JsonArray songs)
                    throw new InvalidDataException($"Event folder {folderId} has no songNo array.");
                for (var songIndex = 0; songIndex < songs.Count; songIndex++)
                    if (ReadInt(songs[songIndex]) == null)
                        throw new InvalidDataException($"Event folder {folderId} contains a non-numeric songNo at position {songIndex + 1}.");
            }
        }

        private void EnsureRoundTrip(string path)
        {
            var reloaded = Load(path);
            if (!JsonNode.DeepEquals(items, reloaded.items))
                throw new InvalidDataException($"{Path.GetFileName(path)} did not round-trip to the same event-folder data.");
        }

        private static int? ReadInt(JsonNode node)
        {
            if (node is not JsonValue value) return null;
            if (value.TryGetValue<int>(out var integer)) return integer;
            if (value.TryGetValue<long>(out var longInteger) && longInteger >= int.MinValue && longInteger <= int.MaxValue)
                return (int)longInteger;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out integer)) return integer;
            return null;
        }

        private static byte[] Decompress(byte[] bytes, Func<Stream, Stream> createDecompressor)
        {
            using var input = new MemoryStream(bytes, writable: false);
            using var decompressor = createDecompressor(input);
            using var output = new MemoryStream();
            decompressor.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] Compress(byte[] bytes, Func<Stream, Stream> createCompressor)
        {
            using var output = new MemoryStream();
            using (var compressor = createCompressor(output))
                compressor.Write(bytes, 0, bytes.Length);
            return output.ToArray();
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            var temporary = path + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
