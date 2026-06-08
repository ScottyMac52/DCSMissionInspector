namespace DcsMissionReader.Models
{
    public sealed class PostBriefingKmlOptions
    {
        /// <summary>
        /// Maximum number of track points to include per object in the generated KML file. This property allows for limiting the number of position samples that are included for each tracked object in the KML output, which can help reduce the file size and improve performance when visualizing the KML file in mapping applications. By setting a maximum number of track points, users can ensure that the KML file remains manageable and does not become excessively large due to long tracks with many position samples. The default value is set to 75, but it can be adjusted based on user preferences or specific requirements for the post-briefing analysis.
        /// </summary>
        public int MaxTrackPointsPerObject { get; init; } = 75;

        /// <summary>
        /// If true (usually for non-US theatres), treat Tacview Enemies as Red, even if Tacview reports them as Neutral. This option allows for a more intuitive color-coding of objects in the generated KML file based on their coalition affiliation, especially in cases where Tacview's labeling may not align with the expected DCS coalition designations. By treating Tacview Enemies as Red, users can have a clearer visual distinction between friendly and hostile forces in the KML visualization, which can enhance the analysis of the mission's post-briefing data. This is particularly useful in non-US theatres where the coalition affiliations may differ from the standard US-centric color-coding conventions.
        /// </summary>
        public bool TreatTacviewEnemiesAsRed { get; init; } = false;

        /// <summary>
        /// If true (usually for non-US theatres), treat Tacview Allies as Blue and Enemies as Red, even if Tacview reports them as Neutral or Enemies. This option allows for a more intuitive color-coding of objects in the generated KML file based on their coalition affiliation, especially in cases where Tacview's labeling may not align with the expected DCS coalition designations. By treating Tacview Allies as Blue and Enemies as Red, users can have a clearer visual distinction between friendly and hostile forces in the KML visualization, which can enhance the analysis of the mission's post-briefing data. This is particularly useful in non-US theatres where the coalition affiliations may differ from the standard US-centric color-coding conventions.
        /// </summary>
        public bool TreatTacviewAlliesAsBlue { get; init; } = false;

        /// <summary>
        /// Keep the default Tacview color coding for known US naval assets, which may be reported as Neutral in Tacview but are actually friendly (Blue) in DCS. This option allows for maintaining the default color-coding of known US naval assets in the generated KML file, even if Tacview reports them as Neutral. By keeping these assets colored according to their actual coalition affiliation (Blue for friendly), users can have a more accurate visual representation of the forces involved in the mission, which can enhance the analysis of the mission's post-briefing data. This is particularly important for US theatres where naval assets may be misclassified in Tacview but are clearly identifiable as friendly forces in DCS.
        /// </summary>
        public bool InferBlueForKnownUsNavalAssets { get; init; } = true;
    }
}