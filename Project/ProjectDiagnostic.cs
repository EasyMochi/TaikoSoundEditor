namespace TaikoSoundEditor.Project
{
    internal enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    internal sealed class ProjectDiagnostic
    {
        public ProjectDiagnostic(DiagnosticSeverity severity, string songId, string component, string message)
        {
            Severity = severity;
            SongId = songId ?? string.Empty;
            Component = component ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public DiagnosticSeverity Severity { get; }
        public string SongId { get; }
        public string Component { get; }
        public string Message { get; }

        public string Signature => $"{Severity}|{SongId}|{Component}|{Message}";

        public override string ToString() => $"[{Severity}] {SongId} | {Component} | {Message}";
    }
}
