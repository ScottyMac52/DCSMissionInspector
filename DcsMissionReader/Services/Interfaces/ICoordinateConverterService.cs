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
    }
}
