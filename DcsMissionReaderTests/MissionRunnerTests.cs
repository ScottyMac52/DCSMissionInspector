using DcsMissionReader;
using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using Moq;

namespace DcsMissionReaderTests
{
    public class MissionRunnerTests
    {
        private readonly Mock<ICommandLineOptionsService> _mockCmdService;
        private readonly Mock<IMissionProcessor> _mockProcessor;
        private readonly MissionRunner _runner;

        public MissionRunnerTests()
        {
            _mockCmdService = new Mock<ICommandLineOptionsService>();
            _mockProcessor = new Mock<IMissionProcessor>();
            _runner = new MissionRunner(_mockCmdService.Object, _mockProcessor.Object);
        }

        [Fact]
        public async Task RunAsync_WithInvalidArgs_ReturnsNegativeOne()
        {
            // Arrange
            _mockCmdService.Setup(c => c.Parse(It.IsAny<string[]>()))
                           .Returns(new AppOptions());

            // Act
            int exitCode = await _runner.RunAsync(new[] { "--invalid" });

            // Assert
            Assert.Equal(-1, exitCode);
            _mockProcessor.Verify(p => p.ProcessAsync(It.IsAny<AppOptions>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_WithValidArgs_ExecutesProcessorAndReturnsZero()
        {
            // Arrange
            var validOptions = new AppOptions { MissionFiles = ["test.miz"] };

            _mockCmdService.Setup(c => c.Parse(It.IsAny<string[]>()))
                           .Returns(validOptions);

            _mockProcessor.Setup(p => p.ProcessAsync(validOptions))
                          .Returns(Task.CompletedTask);

            // Act
            int exitCode = await _runner.RunAsync(new[] { "-f", "test.miz" });

            // Assert
            Assert.Equal(0, exitCode);
            _mockProcessor.Verify(p => p.ProcessAsync(validOptions), Times.Once);
        }
    }
}