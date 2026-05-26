using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using Microsoft.Win32;
using Moq;

namespace DcsMissionReaderTests
{

    public class RegistryManagementServiceTests
    {
        [Fact]
        public void IsRegistered_ShouldReturnTrue_WhenKeyExists()
        {
            // Arrange
            var mockRegistry = new Mock<IRegistryWrapper>();
            mockRegistry.Setup(x => x.OpenSubKey(It.IsAny<string>()))
                        .Returns(Registry.CurrentUser); // Return a fake handle

            var service = new RegistryManagementService(mockRegistry.Object);

            // Act
            var result = service.IsRegistered();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRegistered_ShouldReturnFalse_WhenKeyDoesNotExist()
        {
            // Arrange
            var mockRegistry = new Mock<IRegistryWrapper>();
            mockRegistry.Setup(x => x.OpenSubKey(It.IsAny<string>()))
                        .Returns((RegistryKey?)null); // Return null to simulate missing key

            var service = new RegistryManagementService(mockRegistry.Object);

            // Act
            var result = service.IsRegistered();

            // Assert
            Assert.False(result);
        }


        [Fact]
        public void Install_ShouldThrowException_WhenNotAdmin()
        {
            // Arrange
            var mockIdentity = new Mock<IIdentityService>();
            mockIdentity.Setup(x => x.IsAdministrator()).Returns(false);
            var service = new RegistryManagementService(null, mockIdentity.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => service.Install());
        }

        [Fact]
        public void Install_ShouldCallSetValue_WhenAdmin()
        {
            // Arrange
            var mockRegistry = new Mock<IRegistryWrapper>();
            var mockIdentity = new Mock<IIdentityService>();

            mockIdentity.Setup(x => x.IsAdministrator()).Returns(true);

            var service = new RegistryManagementService(mockRegistry.Object, mockIdentity.Object);

            // Act
            service.Install();

            // Assert
            // Verify that SetValue was called for the Parent Menu
            mockRegistry.Verify(x => x.SetValue(
                It.Is<string>(s => s.Contains(@"miz_auto_file\shell\DCS")),
                "SubCommands",
                "DCS.HTML;DCS.KML;DCS.JSON"), Times.Once);

            // Verify SetValue was called for the individual commands
            // We check for the specific paths for HTML, KML, and JSON
            mockRegistry.Verify(x => x.SetValue(It.Is<string>(s => s.Contains("DCS.HTML")), null, "Create HTML"), Times.Once);
            mockRegistry.Verify(x => x.SetValue(It.Is<string>(s => s.Contains("DCS.HTML\\command")), null, It.Is<string>(p => p.Contains("--html"))), Times.Once);
        }

        [Fact]
        public void Install_ShouldCallSetValue_WithCorrectArguments()
        {
            // Arrange
            var mockRegistry = new Mock<IRegistryWrapper>();
            var mockIdentity = new Mock<IIdentityService>();
            mockIdentity.Setup(x => x.IsAdministrator()).Returns(true);

            var service = new RegistryManagementService(mockRegistry.Object, mockIdentity.Object);

            // Act
            service.Install();

            // Assert: Verify that the path and value were passed correctly
            mockRegistry.Verify(x => x.SetValue(
                It.Is<string>(s => s.Contains("DCS.HTML")),
                null,
                "Create HTML"), Times.Once);
        }
    }
}
