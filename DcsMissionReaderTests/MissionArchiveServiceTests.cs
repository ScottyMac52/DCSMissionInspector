using DcsMissionReader.Services;
using System.IO.Compression;
using Xunit;

namespace DcsMissionReaderTests
{
    public class MissionArchiveServiceTests : IDisposable
    {
        private readonly string _tempZipPath;

        public MissionArchiveServiceTests()
        {
            _tempZipPath = Path.Combine(Path.GetTempPath(), "test_mission.miz");
        }

        [Fact]
        public async Task GetMissionContentAsync_ReturnsContent_WhenMissionEntryExists()
        {
            // Arrange
            string expectedContent = "mission_data_here";
            using (ZipArchive archive = ZipFile.Open(_tempZipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("mission");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(expectedContent);
            }

            var service = new MissionArchiveService();

            // Act
            var result = await service.GetMissionContentAsync(_tempZipPath);

            // Assert
            Assert.Equal(expectedContent, result);
        }

        [Fact]
        public async Task GetMissionContentAsync_ThrowsFileNotFoundException_WhenMissionEntryMissing()
        {
            // Arrange
            using (ZipArchive archive = ZipFile.Open(_tempZipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("wrong_file.txt");
            }

            var service = new MissionArchiveService();

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.GetMissionContentAsync(_tempZipPath));
        }

        public void Dispose()
        {
            if (File.Exists(_tempZipPath)) File.Delete(_tempZipPath);
        }
    }
}