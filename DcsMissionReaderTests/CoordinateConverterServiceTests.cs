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

        [Fact]
        public void Convert_WhenOffsetsProvided_CalculatesCorrectPosition()
        {
            // Arrange
            string theatre = "persiangulf"; // Origin (25.0, 55.0)
            double dcsX = 111132.92; // Approx 1 degree of latitude
            double dcsY = 111412.84 * Math.Cos(26.0 * Math.PI / 180.0); // Approx 1 degree of longitude at 26deg

            // Act
            var result = _service.Convert(dcsX, dcsY, theatre);

            // Assert
            // Expecting roughly +1.0 lat and +1.0 lon from the 25, 55 origin
            Assert.InRange(result.lat, 25.99, 26.01);
            Assert.InRange(result.lon, 55.99, 56.01);
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