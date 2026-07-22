using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal sealed class SongRecord
    {
        public string Id { get; set; }
        public int UniqueId { get; set; }
        public bool HasUra { get; set; }
        public JsonObject MusicInfo { get; set; }
        public JsonObject MusicAttribute { get; set; }
        public List<JsonObject> MusicOrders { get; } = new List<JsonObject>();
        public List<JsonObject> MusicAiSections { get; } = new List<JsonObject>();
        public List<JsonObject> MusicUsbSettings { get; } = new List<JsonObject>();
        public JsonObject TitleWord { get; set; }
        public JsonObject SubtitleWord { get; set; }
        public JsonObject DetailWord { get; set; }
        public SongAssets Assets { get; set; }
    }
}
