using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class CoordinateConversionTests
    {
        [Theory]
        [MemberData(nameof(AllRegisteredTerrainNames))]
        public void ConvertMissionXYToLatLon_ForRegisteredTerrain_AtOrigin_ReturnsFiniteValidCoordinate(
            string terrainName)
        {
            // Act
            var result = DcsCoordinateConverter.ConvertMissionXYToLatLon(
                terrainName,
                missionX: 0,
                missionY: 0);

            // Assert
            AssertValidLatLon(result.lat, result.lon);
        }

        [Theory]
        [MemberData(nameof(AllRegisteredTerrainNames))]
        public void ConvertMissionXYToLatLon_ForRegisteredTerrain_AtRepresentativePoints_ReturnsFiniteValidCoordinates(
            string terrainName)
        {
            var testPoints = new[]
            {
            new TestPoint(0, 0),
            new TestPoint(100_000, 100_000),
            new TestPoint(100_000, -100_000),
            new TestPoint(-100_000, 100_000),
            new TestPoint(-100_000, -100_000),
            new TestPoint(500_000, 500_000),
            new TestPoint(500_000, -500_000),
            new TestPoint(-500_000, 500_000),
            new TestPoint(-500_000, -500_000)
        };

            foreach (var point in testPoints)
            {
                var result = DcsCoordinateConverter.ConvertMissionXYToLatLon(
                    terrainName,
                    missionX: point.X,
                    missionY: point.Y);

                AssertValidLatLon(result.lat, result.lon);
            }
        }

        [Theory]
        [MemberData(nameof(KnownMissionEditorCoordinates))]
        public void ConvertMissionXYToLatLon_ForKnownMissionEditorCoordinates_MatchesDcsMissionEditor(
            string terrainName,
            double missionX,
            double missionY,
            double expectedLat,
            double expectedLon)
        {
            // Act
            var result = DcsCoordinateConverter.ConvertMissionXYToLatLon(
                terrainName,
                missionX,
                missionY);

            // Assert
            // 0.00001 degrees is roughly 1.1 meters latitude.
            // Longitude distance varies by latitude, but this is still a tight DCS-level tolerance.
            Assert.InRange(result.lat, expectedLat - 0.00001, expectedLat + 0.00001);
            Assert.InRange(result.lon, expectedLon - 0.00001, expectedLon + 0.00001);
        }

        [Fact]
        public void ConvertMissionXYToLatLon_ForUnknownTerrain_ThrowsNotSupportedException()
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                DcsCoordinateConverter.ConvertMissionXYToLatLon(
                    "TotallyFakeTerrain",
                    missionX: 0,
                    missionY: 0));

            Assert.Contains("TotallyFakeTerrain", exception.Message);
        }

        [Fact]
        public void TerrainProjectionRegistry_ContainsOnlyValidProjectionDefinitions()
        {
            Assert.NotEmpty(TerrainProjectionRegistry.Projections);

            foreach (var pair in TerrainProjectionRegistry.Projections)
            {
                var terrainName = pair.Key;
                var projection = pair.Value;

                Assert.False(string.IsNullOrWhiteSpace(terrainName));
                Assert.False(string.IsNullOrWhiteSpace(projection.Name));

                Assert.Equal(ProjectionKind.TransverseMercator, projection.Projection);

                Assert.InRange(projection.CentralMeridianDegrees, -180.0, 180.0);
                Assert.InRange(projection.ScaleFactor, 0.1, 10.0);

                Assert.True(double.IsFinite(projection.FalseEasting));
                Assert.True(double.IsFinite(projection.FalseNorthing));
                Assert.True(double.IsFinite(projection.SemiMajorAxis));
                Assert.True(double.IsFinite(projection.Flattening));

                Assert.True(projection.SemiMajorAxis > 0);
                Assert.True(projection.Flattening > 0);
                Assert.True(projection.Flattening < 1);

                Assert.True(
                    projection.AxisMapping is DcsAxisMapping.XIsEasting_ZIsNorthing
                        or DcsAxisMapping.XIsNorthing_ZIsEasting,
                    $"Unsupported axis mapping for terrain '{projection.Name}'.");
            }
        }

        public static IEnumerable<object[]> AllRegisteredTerrainNames()
        {
            return TerrainProjectionRegistry.Projections
                .Keys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .Select(name => new object[] { name });
        }

        public static IEnumerable<object[]> KnownMissionEditorCoordinates()
        {
            // Persian Gulf validation point from DCS Mission Editor:
            //
            // Metric:
            // X +00448865
            // Z -00730142
            //
            // Lat Long Precise:
            // N 29°57'36.99"
            // E 48°39'44.50"

            yield return new object[]
            {
            "PersianGulf",
            448_865.0,
            -730_142.0,
            DmsToDecimal(29, 57, 36.99),
            DmsToDecimal(48, 39, 44.50)
            };

            // Add more Mission Editor golden points here as you validate each terrain.
            //
            // Example format:
            //
            // yield return new object[]
            // {
            //     "Syria",
            //     missionX,
            //     missionY,
            //     DmsToDecimal(latDeg, latMin, latSec),
            //     DmsToDecimal(lonDeg, lonMin, lonSec)
            // };
        }

        private static void AssertValidLatLon(double lat, double lon)
        {
            Assert.True(double.IsFinite(lat), $"Latitude is not finite: {lat}");
            Assert.True(double.IsFinite(lon), $"Longitude is not finite: {lon}");

            Assert.InRange(lat, -90.0, 90.0);
            Assert.InRange(lon, -180.0, 180.0);
        }

        private static double DmsToDecimal(
            int degrees,
            int minutes,
            double seconds)
        {
            double sign = degrees < 0 ? -1.0 : 1.0;

            return sign *
                (
                    Math.Abs(degrees)
                    + minutes / 60.0
                    + seconds / 3600.0
                );
        }

        private readonly record struct TestPoint(
            double X,
            double Y);
    }
}
