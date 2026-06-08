using System.IO.Compression;
using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the IMissionArchiveService interface to read DCS mission files and resources from a .miz archive. This class is responsible for extracting the 'mission' file content from the .miz archive, which is a ZIP file, and returning it as a string for further processing. It uses the System.IO.Compression namespace to handle ZIP file operations and ensures that the mission content is properly read and returned for use in the mission processing workflow.
    /// </summary>
    public class MissionArchiveService : IMissionArchiveService
    {
        /// <summary>
        /// Extracts the 'mission' file content from the specified .miz archive and returns it as a string. This method opens the .miz file as a ZIP archive, locates the 'mission' entry, and reads its content asynchronously. If the 'mission' file is not found within the archive, it throws a FileNotFoundException to indicate that the required mission data is missing. This method is essential for retrieving the core mission data needed for processing and exporting based on the provided options.
        /// </summary>
        /// <param name="zipFilePath">The path to the .miz archive file.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the content of the 'mission' file as a string.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the 'mission' file is not found within the archive.   </exception>
        public async Task<string> GetMissionContentAsync(string zipFilePath)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            var entry = archive.GetEntry("mission");
            if (entry == null)
                throw new FileNotFoundException("Mission file not found in archive.");

            using var reader = new StreamReader(entry.Open());
            return await reader.ReadToEndAsync();
        }

        public void ExtractToDirectory(string zipFilePath, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            ZipFile.ExtractToDirectory(zipFilePath, destinationDirectory, overwriteFiles: true);
        }
    }
}