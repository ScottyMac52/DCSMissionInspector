namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Defines an interface for services that handle DCS mission archive files (e.g., .miz files).
    /// </summary>
    public interface IMissionArchiveService
    {
        /// <summary>
        /// Returns the primary 'mission' file content as a stream or string.
        /// </summary>
        /// <param name="zipFilePath">The path to the .miz file.</param>
        /// <returns>The content of the mission file.</returns>
        Task<string> GetMissionContentAsync(string zipFilePath);

        /// <summary>
        /// Extracts the entire contents of the specified .miz archive to the given destination directory. This method is useful for scenarios where additional resources from the archive are needed for processing, such as when handling post-briefing data or when a full export of mission resources is required. The method ensures that the destination directory is created if it does not exist and that existing files are overwritten if necessary.
        /// </summary>
        /// <param name="zipFilePath"></param>
        /// <param name="destinationDirectory"></param>
        void ExtractToDirectory(string zipFilePath, string destinationDirectory);
    }
}
