using DcsMissionReader.Services;
using Xunit;

namespace DcsMissionReader.Tests
{
    public class CommandLineOptionsServiceTests
    {
        private readonly CommandLineOptionsService _service;

        public CommandLineOptionsServiceTests()
        {
            _service = new CommandLineOptionsService();
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]

        public void Parse_WhenHelpFlagPassed_SetsShowHelpToTrue(string flag)
        {
            // Arrange
            string[] args = { flag };

            // Act
            var result = _service.Parse(args);

            // Assert
            Assert.True(result.ShowHelp);
        }

        [Theory]
        [InlineData("--version")]
        [InlineData("-v")]
        [InlineData("--ver")]
        public void Parse_WhenVersionFlagPassed_SetsShowVersionToTrue(string flag)
        {
            // Arrange
            string[] args = { flag };

            // Act
            var result = _service.Parse(args);

            // Assert
            Assert.True(result.ShowVersion);
        }

        [Theory]
        [InlineData("--json", true)]
        [InlineData("-j", true)]
        [InlineData("--out-json", true)]
        public void Parse_WhenJsonFlagPassed_SetsCreateJsonToTrue(string flag, bool expected)
        {
            // Act
            var result = _service.Parse([flag]);

            // Assert
            Assert.Equal(expected, result.CreateJson);
        }

        [Theory]
        [InlineData("--kml", true)]
        [InlineData("-k", true)]
        [InlineData("--google-earth", true)]
        public void Parse_WhenKmlFlagPassed_SetsCreateKmlToTrue(string flag, bool expected)
        {
            // Act
            var result = _service.Parse([flag]);

            // Assert
            Assert.Equal(expected, result.CreateKml);
        }

        [Theory]
        [InlineData("--html", true)]
        [InlineData("--out-html", true)]
        [InlineData("--create-html", true)]
        public void Parse_WhenHtmlFlagPassed_SetsCreateHtmlToTrue(string flag, bool expected)
        {
            // Act
            var result = _service.Parse([flag]);

            // Assert
            Assert.Equal(expected, result.CreateHtml);
        }


        [Theory]
        [InlineData("-f", true)]
        [InlineData("--full", true)]
        [InlineData("--full-export", true)]
        public void Parse_WhenFullFlagPassed_SetsFullExportToTrue(string flag, bool expected)
        {
            // Act
            var result = _service.Parse([flag]);

            // Assert
            Assert.Equal(expected, result.FullExport);
        }

        [Theory]
        [InlineData("--check", true)]
        [InlineData("-c", true)]
        [InlineData("--check-registration", true)]
        public void Parse_WhenCheckFlagPassed_SetsCheckRegistrationToTrue(string flag, bool expected)
        {
            // Act
            var result = _service.Parse([flag]);

            // Assert
            Assert.Equal(expected, result.CheckRegistration);
        }

        [Theory]
        [InlineData("--install", true)]
        [InlineData("-i", true)]
        [InlineData("--install-registration", true)]
        public void Parse_WhenInstallFlagPassed_SetsInstallRegistrationToTrue(string flag, bool expected)
        {
            // Act
            var result = _service.Parse([flag]);

            // Assert
            Assert.Equal(expected, result.InstallRegistration);
        }

        [Theory]
        [InlineData("--uninstall", true)]
        [InlineData("-u", true)]
        [InlineData("--uninstall-registration", true)]
        public void Parse_WhenUninstallFlagPassed_SetsUninstallRegistrationToTrue(string flag, bool expected)
        {
            // Act
            var result = _service.Parse([flag]);

            // Assert
            Assert.Equal(expected, result.UninstallRegistration);
        }

        [Fact]
        public void Parse_WhenMizFilesProvided_AreAddedToMissionFilesList()
        {
            // Arrange
            string[] args = { "mission1.miz", "test.txt", "mission2.miz" };

            // Act
            var result = _service.Parse(args);

            // Assert
            Assert.Contains("mission1.miz", result.MissionFiles);
            Assert.Contains("mission2.miz", result.MissionFiles);
            Assert.DoesNotContain("test.txt", result.MissionFiles);
        }
    }
}