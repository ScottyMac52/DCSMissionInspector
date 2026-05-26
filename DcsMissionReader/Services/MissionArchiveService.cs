using System.IO.Compression;
using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services
{
    public class MissionArchiveService : IMissionArchiveService
    {
        // Extracts the 'mission' file content as a string
        public async Task<string> GetMissionContentAsync(string zipFilePath)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            var entry = archive.GetEntry("mission");
            if (entry == null)
                throw new FileNotFoundException("Mission file not found in archive.");

            using var reader = new StreamReader(entry.Open());
            return await reader.ReadToEndAsync();
        }
    }
}