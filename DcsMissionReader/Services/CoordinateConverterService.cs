using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the ICoordinateConverterService interface to provide functionality for converting DCS X/Y coordinates (in meters) to geographic coordinates (latitude and longitude) based on the theatre's reference coordinates. The service uses a static dictionary to store reference coordinates for various theatres, allowing for flexible conversion based on the specified theatre. If a theatre is not found in the dictionary, a default reference point is used as a fallback. The conversion is performed using a simple equirectangular approximation, which is suitable for small areas and provides a straightforward way to convert between coordinate systems.
    /// </summary>
    public class CoordinateConverterService : ICoordinateConverterService
    {
        // Persian Gulf specific constants for the Transverse Mercator projection used by DCS for that map
        private const double PG_CENTRAL_MERIDIAN = 57.0;
        private const double PG_FALSE_EASTING = 75755.99999999645;
        private const double PG_FALSE_NORTHING = -2894933.0000000377;
        private const double PG_K0 = 0.9996;           // scale factor

        private const double WGS84_A = 6378137.0;                    // semi-major axis
        private const double WGS84_F = 1.0 / 298.257223563;          // flattening
        private const double WGS84_E2 = WGS84_F * (2 - WGS84_F);     // e²

        // WGS84 Ellipsoid Constants (Global math constants, NOT map-specific hardcodes)
        private const double SemiMajorAxisA = 6378137.0;
        private const double FlatteningF = 1 / 298.257223563;
        private const double SemiMinorAxisB = SemiMajorAxisA * (1 - FlatteningF);

        /// <summary>
        /// Static dictionary mapping theatre names to their reference latitude and longitude coordinates. This serves as the anchor point for converting DCS X/Y coordinates (in meters) to geographic coordinates (latitude and longitude) using an equirectangular approximation. The dictionary is case-insensitive, allowing for flexible input of theatre names. If a theatre name is not found in the dictionary, a default anchor point of (42.0, 42.0) is used as a fallback.
        /// </summary>
        private static readonly Dictionary<string, (double lat, double lon)> TheatreAnchors = new(StringComparer.OrdinalIgnoreCase)
        {
            { "caucasus", (45.0355, 34.2287) },
            { "persiangulf", (25.0, 55.0) },
            { "syria", (34.0, 38.0) },
            { "marianas", (13.5, 144.5) },
            { "marianaislands", (13.5, 144.5) },
            { "ww2marianas", (13.5, 144.5) },
            { "marianaislands_ww2", (13.5, 144.5) },
            { "normandy", (49.0, 0.0) },
            { "thechannel", (51.0, 0.0) },
            { "sinai", (29.0, 33.0) },
            { "iraq", (33.0, 44.0) },
            { "kola", (68.0, 30.0) },
            { "afghanistan", (34.0, 66.0) },
            { "germany", (50.0, 10.0) },
            { "coldwargermany", (50.0, 10.0) },
            { "southatlantic", (-52.482100, -59.141840) },
            { "south atlantic", (-52.482100, -59.141840) },
            { "falklands", (-52.482100, -59.141840) }
        };

        /// <summary>
        /// Converts DCS X/Y coordinates (in meters) to latitude and longitude based on the specified theatre's reference coordinates. If the theatre is identified as the Persian Gulf map, a specialized conversion method is used to account
        /// </summary>
        /// <param name="dcsX">The X coordinate in DCS meters.</param>
        /// <param name="dcsY">The Y coordinate in DCS meters.</param>
        /// <param name="theatre">The theatre name for determining the reference coordinates.</param>
        /// <param name="highAccuracy">Whether to use high accuracy conversion (default: true).</param>
        /// <returns>A tuple containing the latitude and longitude.</returns>
        public (double lat, double lon) Convert(double dcsX, double dcsY, string theatre, bool highAccuracy = true)
        {
            if (IsPersianGulf(theatre))
                return ConvertPersianGulf(dcsX, dcsY, highAccuracy);

            // Everything below stays 100% unchanged for all other theaters
            (double lat, double lon) anchor = (42.0, 42.0);
            if (!TheatreAnchors.TryGetValue(theatre, out anchor))
                anchor = (42.0, 42.0);

            return ConvertGeneric(dcsX, dcsY, anchor.lat, anchor.lon);
        }

        /// <summary>
        /// Converts DCS X/Y (meters) to lat/lon using a simple equirectangular approximation, relative to a given origin point.
        /// </summary>
        /// <param name="dcsX">The X coordinate in DCS meters.</param>
        /// <param name="dcsY">The Y coordinate in DCS meters.</param>
        /// <param name="originX">The origin X coordinate in DCS meters.</param>
        /// <param name="originY">The origin Y coordinate in DCS meters.</param>
        /// <param name="theatre">The theatre name for determining the reference coordinates.</param>
        /// <returns>A tuple containing the latitude and longitude.</returns>
        public (double lat, double lon) ConvertRelative(double dcsX, double dcsY, double originX, double originY, string theatre)
        {
            (double lat, double lon) anchor = (42.0, 42.0);

            if (!TheatreAnchors.TryGetValue(theatre, out anchor))
            {
                anchor = (42.0, 42.0);
            }

            // Pass the delta into the static method
            return ConvertGeneric(dcsX - originX, dcsY - originY, anchor.lat, anchor.lon);
        }

        /// <summary>
        /// Determines if the given theatre name corresponds to the Persian Gulf map, using a case-insensitive comparison and allowing for common variations in naming (e.g., "pg", "persian", "persian_gulf", etc.). This is necessary because the Persian Gulf map uses a unique Transverse Mercator projection in DCS, requiring a different conversion method than the other theatres. The method normalizes the input string by converting it to lowercase and removing common delimiters before checking against known identifiers for the Persian Gulf theatre.
        /// </summary>
        /// <param name="theatre">The theatre name to check.</param>
        /// <returns>True if the theatre is the Persian Gulf map; otherwise, false.</returns>
        private static bool IsPersianGulf(string theatre)
        {
            if (string.IsNullOrWhiteSpace(theatre)) return false;
            var t = theatre.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("-", "");
            return t == "persiangulf" || t == "pg" || t == "persian";
        }

        private static (double lat, double lon) ConvertPersianGulf(double dcsX, double dcsZ, bool highAccuracy = true)
        {
            const double centralMeridian = 57.0;
            const double falseEasting = 75755.99999999645;
            const double falseNorthing = -2894933.0000000377;
            const double k0 = 0.9996;

            const double a = 6378137.0;
            const double f = 1.0 / 298.257223563;
            const double e2 = f * (2 - f);
            const double ePrime2 = e2 / (1.0 - e2);

            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));

            // DCS PG projection axis mapping:
            // DCS X  -> projected northing
            // DCS Z / mission "y" -> projected easting
            double projectedEasting = dcsZ;
            double projectedNorthing = dcsX;

            double x = (projectedEasting - falseEasting) / k0;
            double y = (projectedNorthing - falseNorthing) / k0;

            double mu = y / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));

            double phi1 = mu;

            if (highAccuracy)
            {
                phi1 += (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu)
                      + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu)
                      + (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu)
                      + (1097 * Math.Pow(e1, 4) / 512) * Math.Sin(8 * mu);
            }
            else
            {
                phi1 += (3 * e1 / 2) * Math.Sin(2 * mu);
            }

            double sinPhi1 = Math.Sin(phi1);
            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);

            double N1 = a / Math.Sqrt(1 - e2 * sinPhi1 * sinPhi1);
            double T1 = tanPhi1 * tanPhi1;
            double C1 = ePrime2 * cosPhi1 * cosPhi1;
            double R1 = a * (1 - e2) / Math.Pow(1 - e2 * sinPhi1 * sinPhi1, 1.5);
            double D = x / N1;

            double latRad = phi1 - (N1 * tanPhi1 / R1) *
            (
                D * D / 2
                - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ePrime2) * Math.Pow(D, 4) / 24
                + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ePrime2 - 3 * C1 * C1) * Math.Pow(D, 6) / 720
            );

            double lonRad = centralMeridian * Math.PI / 180.0 +
            (
                D
                - (1 + 2 * T1 + C1) * Math.Pow(D, 3) / 6
                + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * C1 + 24 * T1 * T1) * Math.Pow(D, 5) / 120
            ) / cosPhi1;

            return (latRad * 180.0 / Math.PI, lonRad * 180.0 / Math.PI);
        }

        /// <summary>
        /// High-precision conversion using the WGS 84 Reference Ellipsoid.
        /// Accounts for the Earth's oblateness (equatorial bulge) using Taylor Series trigonometric polynomials.
        /// </summary>
        /// <param name="dcsX">The DCS X coordinate (Northing).</param>
        /// <param name="dcsY">The DCS Y coordinate (Easting).</param>
        /// <param name="refLat">The reference latitude for the conversion.</param>
        /// <param name="refLon">The reference longitude for the conversion.</param>
        /// <param name="invertNorthing">Whether to invert the northing direction.</param>
        /// <returns>A tuple containing the latitude and longitude.</returns>
        public static (double lat, double lon) ConvertGeneric(double dcsX, double dcsY, double refLat, double refLon, bool invertNorthing = false)
        {
            // 1. Convert the reference latitude to radians for Math.Cos
            double refLatRad = refLat * Math.PI / 180.0;

            // 2. WGS 84 EXACT LATITUDE SCALE: Calculate the exact meters in one degree of Latitude
            // at the reference point. (Accounts for the poles being flatter than the equator).
            double metersPerDegLat = 111132.92
                                   - (559.82 * Math.Cos(2 * refLatRad))
                                   + (1.175 * Math.Cos(4 * refLatRad))
                                   - (0.0023 * Math.Cos(6 * refLatRad));

            double latSign = invertNorthing ? -1.0 : 1.0;

            // Calculate the precise Target Latitude
            double lat = refLat + latSign * (dcsX / metersPerDegLat);

            // 3. Convert the NEW Target Latitude to radians
            double targetLatRad = lat * Math.PI / 180.0;

            // 4. WGS 84 EXACT LONGITUDE SCALE: Calculate the exact meters in one degree of Longitude
            // at the specific target latitude. (Accounts for the convergence of meridians).
            double metersPerDegLon = (111412.84 * Math.Cos(targetLatRad))
                                   - (93.5 * Math.Cos(3 * targetLatRad))
                                   + (0.118 * Math.Cos(5 * targetLatRad));

            // Calculate the precise Target Longitude
            double lon = refLon + (dcsY / metersPerDegLon);

            return (lat, lon);
        }

    }
}