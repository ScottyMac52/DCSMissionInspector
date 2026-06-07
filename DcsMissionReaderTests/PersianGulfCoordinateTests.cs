namespace DcsMissionReaderTests
{
    public class PersianGulfCoordinateTests
    {
        [Fact]
        public void ConvertPersianGulf_WithCorrectDcsAxisMapping_ReturnsExpectedLatLon()
        {
            // Arrange
            // DCS mission coordinate sample:
            // x = north/south map coordinate
            // y/z = east/west map coordinate
            double dcsX = -128412.47133601;
            double dcsZ = 7940.8526241175;

            double expectedLat = 25.0125666667;
            double expectedLon = 56.3277500000;

            // Act
            var result = ConvertPersianGulf_Corrected(dcsX, dcsZ);

            // Assert
            Assert.Equal(expectedLat, result.lat, precision: 3);
            Assert.Equal(expectedLon, result.lon, precision: 3);
        }

        [Fact]
        public void ConvertPersianGulf_WithOriginalAxisMapping_DoesNotReturnExpectedLatLon()
        {
            // Arrange
            double dcsX = -128412.47133601;
            double dcsZ = 7940.8526241175;

            double expectedLat = 25.0125666667;
            double expectedLon = 56.3277500000;

            // Act
            var result = ConvertPersianGulf_OriginalAxisOrder(dcsX, dcsZ);

            // Assert
            // This proves the original premise:
            // treating DCS X as easting and DCS Z/Y as northing is wrong for PG.
            Assert.NotEqual(expectedLat, result.lat, precision: 2);
            Assert.NotEqual(expectedLon, result.lon, precision: 2);
        }

        private static (double lat, double lon) ConvertPersianGulf_Corrected(double dcsX, double dcsZ)
        {
            const double centralMeridian = 57.0;
            const double falseEasting = 75755.99999999645;
            const double falseNorthing = -2894933.0000000377;
            const double k0 = 0.9996;

            const double a = 6378137.0;
            const double f = 1.0 / 298.257223563;
            const double e2 = f * (2 - f);
            double ePrime2 = e2 / (1.0 - e2);

            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));

            // Correct DCS PG mapping:
            // DCS X  -> projected northing
            // DCS Z/Y -> projected easting
            double projectedEasting = dcsZ;
            double projectedNorthing = dcsX;

            double x = (projectedEasting - falseEasting) / k0;
            double y = (projectedNorthing - falseNorthing) / k0;

            return InverseTransverseMercator(
                x,
                y,
                centralMeridian,
                a,
                e2,
                ePrime2,
                e1);
        }

        private static (double lat, double lon) ConvertPersianGulf_OriginalAxisOrder(double dcsX, double dcsZ)
        {
            const double centralMeridian = 57.0;
            const double falseEasting = 75755.99999999645;
            const double falseNorthing = -2894933.0000000377;
            const double k0 = 0.9996;

            const double a = 6378137.0;
            const double f = 1.0 / 298.257223563;
            const double e2 = f * (2 - f);
            double ePrime2 = e2 / (1.0 - e2);

            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));

            // Original incorrect mapping:
            // DCS X treated as projected easting
            // DCS Z/Y treated as projected northing
            double projectedEasting = dcsX;
            double projectedNorthing = dcsZ;

            double x = (projectedEasting - falseEasting) / k0;
            double y = (projectedNorthing - falseNorthing) / k0;

            return InverseTransverseMercator(
                x,
                y,
                centralMeridian,
                a,
                e2,
                ePrime2,
                e1);
        }

        private static (double lat, double lon) InverseTransverseMercator(
            double x,
            double y,
            double centralMeridianDegrees,
            double a,
            double e2,
            double ePrime2,
            double e1)
        {
            double mu = y / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));

            double phi1 = mu
                + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu)
                + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu)
                + (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu)
                + (1097 * Math.Pow(e1, 4) / 512) * Math.Sin(8 * mu);

            double sinPhi1 = Math.Sin(phi1);
            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);

            double n1 = a / Math.Sqrt(1 - e2 * sinPhi1 * sinPhi1);
            double t1 = tanPhi1 * tanPhi1;
            double c1 = ePrime2 * cosPhi1 * cosPhi1;
            double r1 = a * (1 - e2) / Math.Pow(1 - e2 * sinPhi1 * sinPhi1, 1.5);
            double d = x / n1;

            double latRad = phi1 - (n1 * tanPhi1 / r1) *
            (
                d * d / 2
                - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * ePrime2) * Math.Pow(d, 4) / 24
                + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * ePrime2 - 3 * c1 * c1) * Math.Pow(d, 6) / 720
            );

            double lonRad = centralMeridianDegrees * Math.PI / 180.0 +
            (
                d
                - (1 + 2 * t1 + c1) * Math.Pow(d, 3) / 6
                + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * c1 + 24 * t1 * t1) * Math.Pow(d, 5) / 120
            ) / cosPhi1;

            return (
                latRad * 180.0 / Math.PI,
                lonRad * 180.0 / Math.PI);
        }
    }
}
