using DcsMissionReader.Models;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for post-briefing services, providing functionality to create KML files from ACMI data. This service is responsible for processing the ACMI data extracted from a DCS mission and generating a KML file that can be used for post-mission analysis and visualization. The CreatePostBriefingKml method takes the path to the ACMI ZIP file and an optional output path for the KML file, and returns a result object containing information about the generated KML file. This allows for flexible integration of post-briefing features into the application, enabling users to easily create KML files for their missions.
    /// </summary>
    public interface IPostBriefingService
    {
        /// <summary>
        /// Creates a KML file for post-briefing analysis based on the provided ACMI ZIP file. This method processes the ACMI data contained in the specified ZIP file, extracts relevant information about group tracks, weapon employments, and weapon results, and generates a KML file that can be used for visualization and analysis of the mission's post-briefing data. The output KML file path can be specified, or if not provided, a default location will be used. The method returns a PostBriefingKmlResult object containing details about the source ACMI ZIP file, the output KML file, and counts of the various elements included in the generated KML. This functionality is essential for users who want to analyze their missions after completion using tools that support KML files.
        /// </summary>
        /// <param name="acmiZipFilePath">Path to the ACMI zipfile</param>
        /// <param name="outputKmlFilePath">Path to the output KML file</param>
        /// <param name="options">Post briefing options</param>
        /// <returns></returns>
        PostBriefingKmlResult CreatePostBriefingKml(
            string acmiZipFilePath,
            string? outputKmlFilePath = null,
            PostBriefingKmlOptions? options = null);
    }
}