using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for identity-related services, providing functionality to determine if the current user has administrative privileges. This can be used to conditionally enable or restrict certain features of the application based on the user's permissions. The implementation of this interface may involve checking the user's roles or group memberships in the operating system or application context to determine if they are an administrator.
    /// </summary>
    public interface IIdentityService
    {
        /// <summary>
        /// Determines whether the current user has administrative privileges. This method typically checks the user's role and returns true if they are an administrator, allowing the application to determine whether it can proceed with certain operations that require elevated permissions or if it needs to prompt the user for elevation. The implementation may involve checking the user's group memberships or roles in the operating system or application context.    
        /// </summary>
        /// <returns>True if the current user has administrative privileges; otherwise, false.</returns>
        bool IsAdministrator();
    }
}
