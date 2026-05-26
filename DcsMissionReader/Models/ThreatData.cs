using System.Text.Json.Serialization;

namespace DcsMissionReader.Models
{
    public class ThreatData
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("DisplayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("DetectionRange")]
        public int DetectionRange { get; set; }

        [JsonPropertyName("ThreatRange")]
        public int ThreatRange { get; set; }
    }
}