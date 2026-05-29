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
    }
}