using DcsMissionReader.Models;
using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using Moq;

namespace DcsMissionReaderTests
{
    public class MissionProcessorTests
    {
        [Fact]
        public async Task Process_ShouldInvokeExportStrategies()
        {
            // Arrange
            var mockArchive = new Mock<IMissionArchiveService>();
            var mockStrategy = new Mock<IMissionExportStrategy>();

            // Return a minimal valid mission string
            mockArchive.Setup(a => a.GetMissionContentAsync(It.IsAny<string>()))
                       .ReturnsAsync("mission = { theatre = 'caucasus' }");

            mockStrategy.Setup(s => s.ShouldExport(It.IsAny<AppOptions>())).Returns(true);

            var processor = new MissionProcessor(
                mockArchive.Object,
                [mockStrategy.Object]
            );

            var options = new AppOptions { MissionFiles = ["test.miz"], CreateHtml = true };

            // Act
            await processor.ProcessAsync(options);

            // Assert: Verify the strategy was actually called
            mockStrategy.Verify(s => s.Export(It.IsAny<MissionContext>()), Times.Once);
        }
    }
}
