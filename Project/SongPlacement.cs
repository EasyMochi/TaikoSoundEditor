using System;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal sealed class SongPlacement
    {
        public SongPlacement(JsonObject row, int sourceIndex)
        {
            Row = row ?? throw new ArgumentNullException(nameof(row));
            SourceIndex = sourceIndex;
        }

        public JsonObject Row { get; }
        public int SourceIndex { get; }
        public string Id => JsonRow.GetString(Row, "id") ?? string.Empty;
        public int UniqueId => JsonRow.GetInt(Row, "uniqueId") ?? 0;
        public int GenreNo => JsonRow.GetInt(Row, "genreNo") ?? -1;
        public int CloseDisplayType => JsonRow.GetInt(Row, "closeDispType") ?? 0;

        public string CategoryName => Enum.IsDefined(typeof(Genre), GenreNo)
            ? ((Genre)GenreNo).ToString()
            : $"Unknown ({GenreNo})";
    }
}
