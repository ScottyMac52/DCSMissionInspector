using DcsMissionReader.Services;

namespace DcsMissionReader.Tests
{
    public class ThreatDatabaseServiceTests
    {
        [Fact]
        public void GetThreatRanges_ShouldReturnCorrectValues_FromProvidedJson()
        {
            // Arrange: Create a controlled file
            string tempFile = Path.GetTempFileName();
            string jsonContent = @"{
                ""ticonderog"": {
                    ""Type"": ""TICONDEROG"",
                    ""DisplayName"": ""CG Ticonderoga"",
                    ""DetectionRange"": 150000,
                    ""ThreatRange"": 100000
                }
            }";
            File.WriteAllText(tempFile, jsonContent);

            try
            {
                // Act
                // We use the constructor we modified to accept a path
                var service = new JsonThreatDatabaseService(tempFile);
                var (det, threat) = service.GetThreatRanges("ticonderog");

                // Assert
                Assert.Equal(150000, det);
                Assert.Equal(100000, threat);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}