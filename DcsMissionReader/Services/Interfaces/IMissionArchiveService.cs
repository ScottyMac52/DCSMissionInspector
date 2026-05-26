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
        /// Gets the content of a specific resource within the mission archive.
        /// </summary>
        /// <param name="resourcePath">The path to the resource within the archive.</param>
        /// <returns>The content of the resource as a byte array.</returns> 
        Task<byte[]> GetResourceAsync(string resourcePath);
    }
}
