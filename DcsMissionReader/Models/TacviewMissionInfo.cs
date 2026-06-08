namespace DcsMissionReader.Models
{
    public sealed class TacviewMissionInfo
    {
        public string? FileType { get; set; }

        public string? FileVersion { get; set; }

        public double? ReferenceLongitude { get; set; }

        public double? ReferenceLatitude { get; set; }

        public DateTimeOffset? ReferenceTimeUtc { get; set; }

        public DateTimeOffset? RecordingTimeUtc { get; set; }

        public string? Title { get; set; }

        public string? DataRecorder { get; set; }

        public string? DataSource { get; set; }

        public string? Author { get; set; }

        public string? Comments { get; set; }

        public string? Category { get; set; }

        public string? Briefing { get; set; }
    }
}
