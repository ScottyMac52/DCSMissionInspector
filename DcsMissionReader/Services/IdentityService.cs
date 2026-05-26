using DcsMissionReader.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements identity-related operations, such as checking for administrator privileges.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class IdentityService : IIdentityService
    {
        /// <summary>
        /// Checks if the current user has administrative privileges. This is important for operations that may require elevated permissions, such as modifying the Windows Registry. The method retrieves the current Windows identity and checks if it belongs to the Administrator role, returning true if the user has administrative privileges and false otherwise.
        /// </summary>
        /// <returns>True if the current user has administrative privileges; otherwise, false.  </returns>
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        public bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
