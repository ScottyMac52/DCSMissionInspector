using DcsMissionReader.Services;
using System.IO.Abstractions.TestingHelpers;

namespace DcsMissionReaderTests
{

    public class FileManagementServiceTests
    {
        [Fact]
        public void CopyKneeboards_ShouldCountFilesCorrectly()
        {
            // Arrange
            var mockFs = new MockFileSystem(new Dictionary<string, MockFileData> {
            { @"C:\temp\Kneeboard\page1.jpg", new MockFileData("data") },
            { @"C:\temp\other\ignored.txt", new MockFileData("ignored") }
        });
            var service = new FileManagementService(mockFs);

            // Act
            int count = service.CopyKneeboards(@"C:\temp", @"C:\report");

            // Assert
            Assert.Equal(1, count);
            Assert.True(mockFs.FileExists(@"C:\report\page1.jpg"));
        }

        [Fact]
        public void CopyImages_ShouldOnlyCopyImageFiles()
        {
            // Arrange
            var mockFs = new MockFileSystem(new Dictionary<string, MockFileData> {
                { @"C:\temp\image1.jpg", new MockFileData("img1") },
                { @"C:\temp\subdir\photo.png", new MockFileData("img2") },
                { @"C:\temp\doc.txt", new MockFileData("ignored") },
                { @"C:\temp\texture.dds", new MockFileData("img3") }
            });

            // CRITICAL: Pre-create the directory so Copy doesn't fail
            mockFs.Directory.CreateDirectory(@"C:\report");

            var service = new FileManagementService(mockFs);

            // Act
            service.CopyImages(@"C:\temp", @"C:\report");

            // Assert
            Assert.True(mockFs.FileExists(@"C:\report\image1.jpg"), "JPG should be copied");
            Assert.True(mockFs.FileExists(@"C:\report\photo.png"), "PNG should be copied");
            Assert.True(mockFs.FileExists(@"C:\report\texture.dds"), "DDS should be copied");
            Assert.False(mockFs.FileExists(@"C:\report\doc.txt"), "TXT should be ignored");
        }
    }
}
