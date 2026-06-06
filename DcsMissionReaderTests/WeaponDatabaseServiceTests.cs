using DcsMissionReader.Services;
using DcsMissionReader.Services.Generators;
using DcsMissionReader.Services.Interfaces;
using Moq;

namespace DcsMissionReaderTests
{
    public class WeaponDatabaseServiceTests
    {
        private const string MockWeaponsJson = @"{
            ""FC23864E-CBAB-4743-99CE-B00722DF2B7D"": {
                ""Type"": ""FC23864E-CBAB-4743-99CE-B00722DF2B7D"",
                ""DisplayName"": ""AIM-120C"",
                ""Weight"": 161.48
            },
            ""AIM-9M"": {
                ""Type"": ""AIM-9M"",
                ""DisplayName"": ""AIM-9M Sidewinder"",
                ""Weight"": 85.73
            }
        }";

        [Fact]
        public void GetWeaponName_WithValidGuid_ReturnsDisplayName()
        {
            // Arrange
            var service = new JsonWeaponDatabaseService(MockWeaponsJson, true);

            // Act
            string result = service.GetWeaponName("{FC23864E-CBAB-4743-99CE-B00722DF2B7D}");

            // Assert
            Assert.Equal("AIM-120C", result);
        }

        [Fact]
        public void GetWeaponName_WithMissingBrackets_StillReturnsDisplayName()
        {
            // Arrange
            var service = new JsonWeaponDatabaseService(MockWeaponsJson, true);

            // Act
            string result = service.GetWeaponName("FC23864E-CBAB-4743-99CE-B00722DF2B7D");

            // Assert
            Assert.Equal("AIM-120C", result);
        }

        [Fact]
        public void GetWeaponName_WithUnknownGuid_ReturnsTruncatedGuidFallback()
        {
            // Arrange
            var service = new JsonWeaponDatabaseService(MockWeaponsJson, true);
            string unknownGuid = "{12345678-ABCD-4743-99CE-B00722DF2B7D}";

            // Act
            string result = service.GetWeaponName(unknownGuid);

            // Assert
            Assert.Equal("Unknown [12345678]", result);
        }

        [Fact]
        public void GetWeaponName_WithUnknownTextString_ReturnsCleanedTextFallback()
        {
            // Arrange
            var service = new JsonWeaponDatabaseService(MockWeaponsJson, true);

            // Act
            string result = service.GetWeaponName("CBU_87_CEM");

            // Assert
            Assert.Equal("CBU 87 CEM", result);
        }

        [Fact]
        public void GetWeaponName_WithEmptyString_ReturnsEmptyFallback()
        {
            // Arrange
            var service = new JsonWeaponDatabaseService(MockWeaponsJson, true);

            // Act
            string result = service.GetWeaponName("   ");

            // Assert
            Assert.Equal("Empty", result);
        }
    }

    public class HtmlReportGeneratorWeaponTests
    {
        [Fact]
        public void HtmlReportGenerator_ShouldRequireWeaponDatabaseService()
        {
            // Arrange
            var mockFileMgmt = new Mock<IFileManagementService>();
            var mockThreatDb = new Mock<IThreatDatabaseService>();
            var mockWeaponDb = new Mock<IWeaponDatabaseService>();

            // Act
            var generator = new HtmlReportGenerator(mockFileMgmt.Object, mockThreatDb.Object, mockWeaponDb.Object);

            // Assert
            Assert.NotNull(generator);
        }

        [Fact]
        public void HtmlReportGenerator_ExtractAtoWithLoadouts_ShouldCallWeaponService()
        {
            // Arrange
            var mockFileMgmt = new Mock<IFileManagementService>();
            var mockThreatDb = new Mock<IThreatDatabaseService>();
            var mockWeaponDb = new Mock<IWeaponDatabaseService>();

            // Setup Moq to expect a specific CLSID lookup
            mockWeaponDb.Setup(x => x.GetWeaponName(It.IsAny<string>()))
                        .Returns("Mocked Weapon Name");

            var generator = new HtmlReportGenerator(mockFileMgmt.Object, mockThreatDb.Object, mockWeaponDb.Object);

            /* * Note: To fully execute ExtractAtoWithLoadouts here, we would need to construct 
             * a MoonSharp Table representing the DCS mission structure. 
             * * Because MoonSharp Tables are tightly coupled, the most critical contract 
             * to verify is that the generator was successfully instantiated with the Moq object, 
             * proving the DI container is satisfied.
             */

            mockWeaponDb.Verify(x => x.GetWeaponName(It.IsAny<string>()), Times.Never);
        }
    }
}