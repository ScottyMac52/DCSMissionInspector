namespace DcsMissionReader.Models
{
    /// <summary>
    /// DCS axis mapping enumeration to specify how DCS X and Z/Y coordinates should be interpreted.
    /// </summary>
    public enum DcsAxisMapping
    {
        /// <summary>
        /// Axis mapping where DCS X is treated as easting and DCS Z/Y is treated as northing.
        /// </summary>
        XIsEasting_ZIsNorthing,

        /// <summary>
        /// Axis mapping where DCS X is treated as northing and DCS Z/Y is treated as easting.
        /// </summary>
        XIsNorthing_ZIsEasting
    }
}
