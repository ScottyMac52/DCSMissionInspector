using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public class CoordinateConverterServiceTests
    {
        private readonly CoordinateConverterService _service;

        public CoordinateConverterServiceTests()
        {
            _service = new CoordinateConverterService();
        }

        [Theory]
        [InlineData("caucasus", 45.0355, 34.2287)]
        [InlineData("SYRIA", 34.0, 38.0)] // Testing case insensitivity
        [InlineData("invalid_theatre", 42.0, 42.0)] // Testing fallback
        public void Convert_WhenCalled_ReturnsExpectedAnchor(string theatre, double expectedLat, double expectedLon)
        {
            // Act: Using 0,0 offset to return exactly the anchor point
            var result = _service.Convert(0, 0, theatre);

            // Assert
            Assert.Equal(expectedLat, result.lat, 4);
            Assert.Equal(expectedLon, result.lon, 4);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Convert_PersianGulf_ReturnsCorrectPosition(bool highAccuracy)
        {
            string theatre = "persiangulf";
            double dcsX = -71200;
            double dcsY = 93489;

            var result = _service.Convert(dcsX, dcsY, theatre, highAccuracy);

            // These match the real carrier location in your mission
            Assert.InRange(result.lat, 24.356, 24.358);
            Assert.InRange(result.lon, 55.920, 55.922);
        }


        [Fact]
        public void ConvertRelative_WhenOriginProvided_CalculatesCorrectDelta()
        {
            // Arrange
            string theatre = "syria"; // Anchor (34, 38)
            double dcsX = 10000;
            double originX = 5000; // Delta X = 5000

            // Act
            var result = _service.ConvertRelative(dcsX, 0, originX, 0, theatre);
            var baseline = _service.Convert(5000, 0, theatre);

            // Assert
            Assert.Equal(baseline.lat, result.lat, 6);
            Assert.Equal(baseline.lon, result.lon, 6);
        }
    }
}