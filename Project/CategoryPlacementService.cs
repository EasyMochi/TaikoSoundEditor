using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal sealed class CategoryPlacementService
    {
        private readonly TaikoProject project;

        public CategoryPlacementService(TaikoProject project)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public IReadOnlyList<SongPlacement> GetPlacements(string songId, int uniqueId)
        {
            return project.MusicOrder.Items
                .OfType<JsonObject>()
                .Select((row, index) => new SongPlacement(row, index))
                .Where(placement =>
                    string.Equals(placement.Id, songId, StringComparison.Ordinal) ||
                    (uniqueId != 0 && placement.UniqueId == uniqueId))
                .OrderBy(placement => placement.GenreNo)
                .ThenBy(placement => placement.SourceIndex)
                .ToList();
        }

        public SongPlacement Add(string songId, int uniqueId, int genreNo, int closeDisplayType = 0)
        {
            if (string.IsNullOrWhiteSpace(songId))
                throw new ArgumentException("A song id is required.", nameof(songId));
            if (genreNo < 0)
                throw new ArgumentOutOfRangeException(nameof(genreNo));
            if (GetPlacements(songId, uniqueId).Any(item => item.GenreNo == genreNo))
                throw new InvalidOperationException($"{songId} is already present in category {genreNo}.");

            var row = new JsonObject
            {
                ["genreNo"] = genreNo,
                ["id"] = songId,
                ["uniqueId"] = uniqueId,
                ["closeDispType"] = closeDisplayType
            };
            project.MusicOrder.Items.Add(row);
            return new SongPlacement(row, project.MusicOrder.Items.Count - 1);
        }

        public void Remove(SongPlacement placement)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            var remaining = GetPlacements(placement.Id, placement.UniqueId)
                .Count(item => !ReferenceEquals(item.Row, placement.Row));
            if (remaining == 0)
                throw new InvalidOperationException("A song must remain in at least one category.");
            project.MusicOrder.Items.Remove(placement.Row);
        }

        public void SetPrimaryGenre(SongRecord song, int genreNo)
        {
            if (song?.MusicInfo == null)
                throw new InvalidOperationException("The song has no musicinfo row.");
            if (!GetPlacements(song.Id, song.UniqueId).Any(item => item.GenreNo == genreNo))
                throw new InvalidOperationException("The primary genre must also be one of the song's category placements.");
            song.MusicInfo["genreNo"] = genreNo;
        }

        public void SetCloseDisplayType(SongPlacement placement, int value)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            placement.Row["closeDispType"] = value;
        }

        public void Move(SongPlacement placement, int offset)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            if (offset == 0) return;

            var rows = project.MusicOrder.Items;
            var current = rows.IndexOf(placement.Row);
            if (current < 0) throw new InvalidOperationException("Placement is no longer part of music_order.");

            var sameCategory = rows.OfType<JsonObject>()
                .Select((row, index) => new { row, index })
                .Where(item => (JsonRow.GetInt(item.row, "genreNo") ?? -1) == placement.GenreNo)
                .OrderBy(item => item.index)
                .ToList();
            var position = sameCategory.FindIndex(item => ReferenceEquals(item.row, placement.Row));
            var targetPosition = position + Math.Sign(offset);
            if (position < 0 || targetPosition < 0 || targetPosition >= sameCategory.Count) return;

            var targetIndex = sameCategory[targetPosition].index;
            rows.RemoveAt(current);
            if (targetIndex > current) targetIndex--;
            rows.Insert(targetIndex, placement.Row);
        }
    }
}
