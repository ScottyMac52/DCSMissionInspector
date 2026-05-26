namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for a service that manages the application's registry entries. This service abstracts the details of how the application interacts with the Windows Registry,
    /// providing methods to check registration status, install, and uninstall the application.
    /// </summary>
    public interface IRegistryManagementService
    {
        /// <summary>
        /// Checks if the current user has administrative privileges. This is important for registry operations, as modifying certain registry keys may require elevated permissions. The method typically checks the user's role and returns true if they are an administrator, allowing the application to determine whether it can proceed with registry modifications or if it needs to prompt the user for elevation.
        /// </summary>
        /// <returns>True if the current user has administrative privileges; otherwise, false.</returns>
        bool IsAdministrator();

        /// <summary>
        /// Determines whether the application is currently registered in the Windows Registry. This typically involves checking for specific registry keys or values that indicate the application's presence and configuration.   
        /// </summary>
        /// <returns>True if the application is registered; otherwise, false.</returns>
        bool IsRegistered();
        
        /// <summary>
        /// Installs the application by creating the necessary registry entries. This method typically writes specific keys and values to the Windows Registry to indicate the application's presence and configuration.
        /// </summary>
        void Install();

        /// <summary>
        /// Uninstalls the application by removing the registry entries created during installation. This method typically deletes specific keys and values from the Windows Registry to clean up the application's presence and configuration.
        /// </summary>  
        void Uninstall();
    }
}
