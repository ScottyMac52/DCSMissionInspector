namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Implements conversion of DCS mission coordinates (X, Y) to geographic coordinates (latitude, longitude) based on the theatre's reference point and scale.
    /// </summary>
    public interface ICoordinateConverterService
    {
        /// <summary>
        /// Converts DCS mission coordinates (X, Y) to geographic coordinates (latitude, longitude) based on the specified theatre.
        /// </summary>
        /// <param name="dcsX">The X coordinate in DCS meters.</param>
        /// <param name="dcsY">The Y coordinate in DCS meters.</param>
        /// <param name="theatre">The theatre name for determining the reference coordinates.</param>
        /// <param name="highAccuracy">Whether to use high accuracy conversion (default: true).</param>
        /// <returns>A tuple containing the latitude and longitude.</returns>
        (double lat, double lon) Convert(double dcsX, double dcsY, string theatre, bool highAccuracy = true);

        /// <summary>
        /// Converts DCS mission coordinates (X, Y) to geographic coordinates (latitude, longitude) based on the specified theatre, using a relative conversion method that accounts for an origin point. This is useful for converting coordinates that are relative to a specific location in the theatre, rather than absolute coordinates.
        /// </summary>
        /// <param name="dcsX">The X coordinate in DCS meters.</param>
        /// <param name="dcsZ">The Z coordinate in DCS meters.</param>
        /// <param name="theatreName">The theatre name for determining the reference coordinates.</param>
        /// <returns>A tuple containing the latitude and longitude.</returns>
        (double Latitude, double Longitude) ConvertDcsToLatLon(double dcsX, double dcsZ, string theatreName);
    }
}
