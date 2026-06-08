using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the ICoordinateConverterService interface to provide functionality for converting DCS X/Y coordinates (in meters) to geographic coordinates (latitude and longitude) based on the theatre's reference coordinates. The service uses a static dictionary to store reference coordinates for various theatres, allowing for flexible conversion based on the specified theatre. If a theatre is not found in the dictionary, a default reference point is used as a fallback. The conversion is performed using a simple equirectangular approximation, which is suitable for small areas and provides a straightforward way to convert between coordinate systems.
    /// </summary>
    public class CoordinateConverterService : ICoordinateConverterService
    {
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
            var projection = TerrainProjectionRegistry.GetProjection(theatre);

            var latLon = DcsCoordinateConverter.ConvertDcsToLatLon(
                projection,
                dcsX,
                dcsY); // mission Y is DCS ground-plane Z

            return latLon;
        }
    }
}