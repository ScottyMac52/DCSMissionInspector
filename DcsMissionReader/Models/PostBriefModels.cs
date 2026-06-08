namespace DcsMissionReader.Models
{
    /// <summary>
    /// Result of the post-briefing KML generation process, containing information about the source ACMI zip file, the output KML file, and counts of group tracks, weapon employments, and weapon results included in the generated KML. This class is used to encapsulate the results of the KML generation process after processing a DCS mission briefing, providing a summary of the key outputs and statistics related to the generated KML file. The properties include the file paths for the source ACMI zip file and the output KML file, as well as counts of group tracks, weapon employments, and weapon results that were included in the generated KML. This information can be useful for logging, debugging, or providing feedback to the user about the results of the KML generation process.
    /// </summary>
    public sealed class PostBriefingKmlResult
    {
        /// <summary>
        /// Location of the source ACMI zip file that was processed to generate the KML output. This property holds the file path to the ACMI zip file that was used as the input for the KML generation process. The ACMI zip file contains the necessary data extracted from the DCS mission briefing, which is then processed to create the KML output. The file path is required for reference and may be used for logging or debugging purposes to identify which source data was used for generating the KML file.
        /// </summary>
        public required string SourceAcmiZipFilePath { get; init; }

        /// <summary>
        /// Location where the generated KML file for the post-briefing report was saved. This property holds the file path to the KML output that was generated from processing the ACMI zip file. The KML file contains the visual representation of the mission's post-briefing data, which can be used for analysis or visualization in mapping applications. The file path is required for reference and may be used for logging or debugging purposes to identify where the generated KML file was saved after processing.
        /// </summary>
        public required string OutputKmlFilePath { get; init; }

        /// <summary>
        /// Count of group tracks included in the generated KML file. This property holds the number of group tracks that were processed and included in the KML output. Group tracks represent the movement and actions of groups of units in the DCS mission, and this count provides a summary of how many such tracks were included in the generated KML file. This information can be useful for logging, debugging, or providing feedback to the user about the content of the generated KML file in terms of group track data.
        /// </summary>
        public required int GroupTrackCount { get; init; }

        /// <summary>
        /// Count of weapon employments included in the generated KML file. This property holds the number of weapon employment events that were processed and included in the KML output. Weapon employments represent instances where weapons were used in the DCS mission, and this count provides a summary of how many such events were included in the generated KML file. This information can be useful for logging, debugging, or providing feedback to the user about the content of the generated KML file in terms of weapon employment data.
        /// </summary>
        public required int WeaponEmploymentCount { get; init; }

        /// <summary>
        /// Count of weapon results included in the generated KML file. This property holds the number of weapon result events that were processed and included in the KML output. Weapon results represent the outcomes of weapon employments, such as hits, misses, or other effects, and this count provides a summary of how many such events were included in the generated KML file. This information can be useful for logging, debugging, or providing feedback to the user about the content of the generated KML file in terms of weapon result data.
        /// </summary>
        public required int WeaponResultCount { get; init; }
    }

    
    public sealed class TacviewObjectTrack
    {
        public required string ObjectId { get; init; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? Group { get; set; }

        public string? ParentObjectId { get; set; }

        public string? Coalition { get; set; }

        public string? Color { get; set; }

        public List<TacviewPositionSample> Samples { get; } = new();

        public bool IsWeapon
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Type))
                {
                    return false;
                }

                return Type.Contains("Weapon", StringComparison.OrdinalIgnoreCase)
                    || Type.Contains("Missile", StringComparison.OrdinalIgnoreCase)
                    || Type.Contains("Bomb", StringComparison.OrdinalIgnoreCase)
                    || Type.Contains("Rocket", StringComparison.OrdinalIgnoreCase)
                    || Type.Contains("Projectile", StringComparison.OrdinalIgnoreCase);
            }
        }

        public TacviewPositionSample? Start => Samples.Count == 0 ? null : Samples[0];

        public TacviewPositionSample? End => Samples.Count == 0 ? null : Samples[^1];
    }

    public sealed class TacviewPositionSample
    {
        public required double TimeSeconds { get; init; }

        public required DateTimeOffset? AbsoluteTimeUtc { get; init; }

        public required double Latitude { get; init; }

        public required double Longitude { get; init; }

        public double? AltitudeMeters { get; init; }
    }

    public sealed class TacviewEventRecord
    {
        public required double TimeSeconds { get; init; }

        public required DateTimeOffset? AbsoluteTimeUtc { get; init; }

        public required string EventType { get; init; }

        public required IReadOnlyList<string> Parts { get; init; }

        public string? Text { get; init; }
    }

    public sealed class TacviewWeaponEmployment
    {
        public required string WeaponObjectId { get; init; }

        public string? WeaponName { get; init; }

        public string? WeaponType { get; init; }

        public string? ParentObjectId { get; init; }

        public string? ParentName { get; init; }

        public required TacviewPositionSample Position { get; init; }
    }

    public sealed class TacviewWeaponResult
    {
        public required string EventType { get; init; }

        public required double TimeSeconds { get; init; }

        public required DateTimeOffset? AbsoluteTimeUtc { get; init; }

        public string? SourceObjectId { get; init; }

        public string? SourceName { get; init; }

        public string? TargetObjectId { get; init; }

        public string? TargetName { get; init; }

        public string? Outcome { get; init; }

        public string? Description { get; init; }

        public TacviewPositionSample? Position { get; init; }
    }
}