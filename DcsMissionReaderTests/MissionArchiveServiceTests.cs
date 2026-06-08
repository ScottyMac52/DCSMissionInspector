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

        [Fact]
        public void ExtractToDirectory_ExtractsDictionaryAndTheatreFiles()
        {
            // Arrange
            string testRoot = Path.Combine(Path.GetTempPath(), "DcsMissionArchiveTests_" + Guid.NewGuid().ToString("N"));
            string mizPath = Path.Combine(testRoot, "test.miz");
            string extractDir = Path.Combine(testRoot, "extract");

            Directory.CreateDirectory(testRoot);

            try
            {
                using (var archive = ZipFile.Open(mizPath, ZipArchiveMode.Create))
                {
                    var missionEntry = archive.CreateEntry("mission");
                    using (var writer = new StreamWriter(missionEntry.Open()))
                    {
                        writer.Write("mission = {}");
                    }

                    var dictionaryEntry = archive.CreateEntry("l10n/DEFAULT/dictionary");
                    using (var writer = new StreamWriter(dictionaryEntry.Open()))
                    {
                        writer.Write("dictionary = { DictKey_descriptionText_1 = 'Actual briefing text' }");
                    }

                    var theatreEntry = archive.CreateEntry("theatre");
                    using (var writer = new StreamWriter(theatreEntry.Open()))
                    {
                        writer.Write("PersianGulf");
                    }
                }

                var service = new MissionArchiveService();

                // Act
                service.ExtractToDirectory(mizPath, extractDir);

                // Assert
                Assert.True(File.Exists(Path.Combine(extractDir, "mission")));
                Assert.True(File.Exists(Path.Combine(extractDir, "l10n", "DEFAULT", "dictionary")));
                Assert.True(File.Exists(Path.Combine(extractDir, "theatre")));
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
            }
        }
        public void Dispose()
        {
            if (File.Exists(_tempZipPath)) File.Delete(_tempZipPath);
        }
    }
}