using MoonSharp.Interpreter;

namespace DcsMissionReader.Models
{
    public class MissionContext
    {
        public string MizPath { get; init; }
        public Table MissionTable { get; init; }
        public Table? DictTable { get; init; }
        public string Theatre { get; init; }
        public string Sortie { get; init; }
        public string TempDir { get; init; }
        public string ReportDir { get; init; }
        public AppOptions Options { get; init; }

        // Helper to easily get map name if needed
        public string MapName => File.Exists(Path.Combine(TempDir, "theatre"))
            ? File.ReadAllText(Path.Combine(TempDir, "theatre")).Trim()
            : "Unknown";
    }
}