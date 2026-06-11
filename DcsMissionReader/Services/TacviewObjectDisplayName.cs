using DcsMissionReader.Models;

/// <summary>
/// Gets the display name for a Tacview object track based on its properties, following the format specified in issue #27. The display name is constructed using the group, pilot, name, and object ID properties of the track, with fallbacks to ensure a meaningful name is generated even if some properties are missing. The method prioritizes the pilot name over the object name when both are available, and includes the group name as a prefix if it exists. If neither the pilot nor the name is available, it falls back to using the object ID as the display name. This approach ensures that each Tacview object track has a clear and informative display name for use in reports and visualizations.
/// </summary>
internal static class TacviewObjectDisplayName
{
    /// <summary>
    /// Gets the display name for a Tacview object track based on its properties, following the format specified in issue #27. The display name is constructed using the group, pilot, name, and object ID properties of the track, with fallbacks to ensure a meaningful name is generated even if some properties are missing. The method prioritizes the pilot name over the object name when both are available, and includes the group name as a prefix if it exists. If neither the pilot nor the name is available, it falls back to using the object ID as the display name. This approach ensures that each Tacview object track has a clear and informative display name for use in reports and visualizations.
    /// </summary>
    /// <param name="track"></param>
    /// <returns></returns>
    public static string GetDisplayName(TacviewObjectTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        string? individualName = !string.IsNullOrWhiteSpace(track.Pilot)
            ? track.Pilot
            : track.Name;

        if (!string.IsNullOrWhiteSpace(track.Group))
        {
            return !string.IsNullOrWhiteSpace(individualName)
                ? $"{track.Group}-{individualName}"
                : track.Group;
        }

        return !string.IsNullOrWhiteSpace(individualName)
            ? individualName
            : track.ObjectId;
    }
}