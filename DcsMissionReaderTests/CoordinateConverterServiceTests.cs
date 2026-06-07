using DcsMissionReader.Models;
using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using Moq;

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