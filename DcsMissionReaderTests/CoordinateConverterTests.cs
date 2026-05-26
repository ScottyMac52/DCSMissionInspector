using DcsMissionReader.Services;
using Xunit;

namespace DcsMissionReaderTests
{
    public class CoordinateConverterTests
    {
        private readonly CoordinateConverterService _converter = new();

        [Theory]
        // Official Theatres
        [InlineData("caucasus", 0, 0, 45.0355, 34.2287)]
        [InlineData("persiangulf", 0, 0, 25.0, 55.0)]
        [InlineData("syria", 0, 0, 34.0, 38.0)]
        // Marianas
        [InlineData("marianas", 0, 0, 13.5, 144.5)]
        [InlineData("marianaislands", 0, 0, 13.5, 144.5)]
        [InlineData("ww2marianas", 0, 0, 13.5, 144.5)]
        [InlineData("marianaislands_ww2", 0, 0, 13.5, 144.5)]
        // Others
        [InlineData("normandy", 0, 0, 49.0, 0.0)]
        [InlineData("thechannel", 0, 0, 51.0, 0.0)]
        [InlineData("sinai", 0, 0, 29.0, 33.0)]
        [InlineData("iraq", 0, 0, 33.0, 44.0)]
        [InlineData("kola", 0, 0, 68.0, 30.0)]
        [InlineData("afghanistan", 0, 0, 34.0, 66.0)]
        [InlineData("germany", 0, 0, 50.0, 10.0)]
        [InlineData("coldwargermany", 0, 0, 50.0, 10.0)]
        // South Atlantic
        [InlineData("southatlantic", 0, 0, -52.4821, -59.1418)]
        [InlineData("south atlantic", 0, 0, -52.4821, -59.1418)]
        [InlineData("falklands", 0, 0, -52.4821, -59.1418)]
        // Fallback
        [InlineData("unknown", 0, 0, 42.0, 42.0)]
        public void Convert_ReturnsExpectedAnchor_AtZero(string theatre, double x, double y, double expectedLat, double expectedLon)
        {
            var (lat, lon) = _converter.Convert(x, y, theatre);
            Assert.Equal(expectedLat, lat, 4);
            Assert.Equal(expectedLon, lon, 4);
        }

        [Fact]
        public void ConvertRelative_CalculatesDisplacementCorrectly()
        {
            // Origin at 1000, 1000. Target at 1100, 1100. Delta is 100, 100.
            // It should match Convert(100, 100, "caucasus")
            var relative = _converter.ConvertRelative(1100, 1100, 1000, 1000, "caucasus");
            var direct = _converter.Convert(100, 100, "caucasus");

            Assert.Equal(direct.lat, relative.lat, 6);
            Assert.Equal(direct.lon, relative.lon, 6);
        }

        [Fact]
        public void ConvertGeneric_MathematicalIntegrity()
        {
            double refLat = 0.0;
            // Calculate the actual meters-per-degree at the equator (0 lat)
            double refLatRad = refLat * Math.PI / 180.0;
            double expectedMetersPerDeg = 111132.92 - (559.82 * Math.Cos(2 * refLatRad))
                                                + (1.175 * Math.Cos(4 * refLatRad))
                                                - (0.0023 * Math.Cos(6 * refLatRad));

            // Act: Move exactly one "degree's worth" of meters
            var result = CoordinateConverterService.ConvertGeneric(expectedMetersPerDeg, 0, refLat, 0);

            // Assert: Should be exactly 1.0
            Assert.Equal(1.0, result.lat, 4);
        }
    }
}
