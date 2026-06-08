namespace DcsMissionReader.Services.Interfaces
{
    public interface IWeaponDatabaseService
    {
        /// <summary>
        /// Gets the display name of a weapon based on its CLSID. If not found, returns the original CLSID.
        /// </summary>
        /// <param name="clsid">The CLSID of the weapon.</param>
        /// <returns>The display name of the weapon or the original CLSID if not found.</returns>
        string GetWeaponName(string clsid);

        /// <summary>
        /// Determines if the provided value is a known weapon CLSID in the database. This method checks if the given CLSID exists in the weapon database and can be used to identify whether a particular weapon is recognized by the system. It returns true if the CLSID is found in the database, indicating that it is a known weapon, and false otherwise. This functionality is essential for validating weapon data during mission processing and ensuring that only recognized weapons are handled appropriately in the application.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool IsKnownWeapon(string value);
    }
}