using DcsMissionReader.Models;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for providing theatre-specific projection parameters, such as reference coordinates and scale factors, which are essential for accurately converting DCS mission coordinates (X, Y) to geographic coordinates (latitude, longitude). This service allows for flexible retrieval of projection parameters based on the theatre name, enabling accurate coordinate conversions across different maps used in DCS missions.
    /// </summary>
    public interface ITheatreProjectionProvider
    {
        /// <summary>
        /// Retrieves the projection parameters for a given theatre, which are necessary for converting DCS mission coordinates to geographic coordinates.
        /// </summary>
        /// <param name="theatreName">The name of the theatre for which to retrieve projection parameters.</param>
        /// <returns>A <see cref="TheatreProjectionParameters"/> object containing the projection parameters for the specified theatre.</returns>
        TheatreProjectionParameters GetParameters(string theatreName);
    }
}
