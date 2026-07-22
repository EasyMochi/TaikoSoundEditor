using System;

namespace TaikoSoundEditor.Project
{
    internal sealed class ProjectRepairAction
    {
        private readonly Action apply;

        public ProjectRepairAction(string songId, string component, string title, string preview, Action apply)
        {
            SongId = songId ?? string.Empty;
            Component = component ?? string.Empty;
            Title = title ?? string.Empty;
            Preview = preview ?? string.Empty;
            this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
        }

        public string SongId { get; }
        public string Component { get; }
        public string Title { get; }
        public string Preview { get; }

        public void Apply() => apply();

        public override string ToString() => $"{SongId} | {Component} | {Title}";
    }
}
