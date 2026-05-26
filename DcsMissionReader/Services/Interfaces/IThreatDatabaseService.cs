namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for a service that provides threat range information for DCS units. This service abstracts the source of the data,
    /// allowing for different implementations, such as reading from a database, a file, or an external API.
    /// </summary>
    public interface IThreatDatabaseService
    {
        /// <summary>
        /// Gets the threat ranges (detection and engagement) for a given unit type. The unit type should be a string that matches the 'type' field in DCS databases.
        /// </summary>
        /// <param name="unitType">The type of the unit for which to retrieve threat ranges.</param>
        /// <returns>A tuple containing the detection range and threat range.</returns>
        (double detection, double threat) GetThreatRanges(string unitType);
    }
}
