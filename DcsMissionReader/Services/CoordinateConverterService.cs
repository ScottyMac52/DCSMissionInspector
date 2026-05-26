using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the ICoordinateConverterService interface to provide functionality for converting DCS X/Y coordinates (in meters) to geographic coordinates (latitude and longitude) based on the theatre's reference coordinates. The service uses a static dictionary to store reference coordinates for various theatres, allowing for flexible conversion based on the specified theatre. If a theatre is not found in the dictionary, a default reference point is used as a fallback. The conversion is performed using a simple equirectangular approximation, which is suitable for small areas and provides a straightforward way to convert between coordinate systems.
    /// </summary>
    public class CoordinateConverterService : ICoordinateConverterService
    {
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
        /// Converts DCS X/Y (meters) to lat/lon using a simple equirectangular approximation, based on the theatre's reference coordinates.
        /// </summary>
        /// <param name="dcsX">The X coordinate in DCS meters.</param>
        /// <param name="dcsY">The Y coordinate in DCS meters.</param>
        /// <param name="theatre">The theatre name for determining the reference coordinates.</param>
        /// <returns>A tuple containing the latitude and longitude.</returns>
        public (double lat, double lon) Convert(double dcsX, double dcsY, string theatre)
        {
            // Explicitly define the default tuple for fallback
            (double lat, double lon) anchor = (42.0, 42.0);

            // Attempt to get from dictionary
            if (!TheatreAnchors.TryGetValue(theatre, out anchor))
            {
                anchor = (42.0, 42.0);
            }

            // Access tuple fields explicitly
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