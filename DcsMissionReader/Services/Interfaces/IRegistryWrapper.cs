using Microsoft.Win32;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for a registry wrapper to abstract away direct interactions with the Windows Registry. This allows for easier testing and separation of concerns by providing a layer of abstraction over the RegistryKey operations. The interface includes methods for opening, creating, and deleting registry subkeys, which can be implemented by a concrete class (e.g., RegistryWrapper) that interacts with the actual Windows Registry. This design enables better testability and maintainability of code that relies on registry operations, as it can be mocked or stubbed in unit tests without affecting the underlying registry access logic.
    /// </summary>
    public interface IRegistryWrapper
    {
        /// <summary>
        /// Opens a subkey with the specified path. This method abstracts the process of accessing registry keys, allowing for easier testing and separation of concerns. The implementation can handle the details of how the registry is accessed, such as which root key to use or how to handle permissions. The method returns a RegistryKey object if the subkey exists, or null if it does not exist or cannot be accessed. This allows calling code to check for the existence of registry keys without needing to directly interact with the Windows Registry API, improving testability and maintainability.  
        /// </summary>
        /// <param name="path">The path of the subkey to open.</param>
        /// <returns>A RegistryKey object if the subkey exists, or null if it does not exist or cannot be accessed.</returns>
        RegistryKey? OpenSubKey(string path);
        /// <summary>
        /// Creates a subkey with the specified path. This method abstracts the process of creating registry keys, allowing for easier testing and separation of concerns. The implementation can handle the details of how the registry is accessed, such as which root key to use or how to handle permissions. The method returns a RegistryKey object representing the newly created subkey.
        /// </summary>
        /// <param name="path">The path of the subkey to create.</param>
        /// <returns>A RegistryKey object representing the newly created subkey.</returns>  
        RegistryKey CreateSubKey(string path);

        /// <summary>
        /// Deletes a subkey tree with the specified path. This method abstracts the process of deleting registry keys, allowing for easier testing and separation of concerns. The implementation can handle the details of how the registry is accessed, such as which root key to use or how to handle permissions. The method takes a boolean parameter 'throwOnMissingSubKey' that determines whether an exception should be thrown if the specified subkey does not exist. If 'throwOnMissingSubKey' is true and the subkey does not exist, an exception will be thrown; otherwise, the method will simply return without throwing an exception.  
        /// </summary>
        /// <param name="path">The path of the subkey to delete.</param>
        /// <param name="throwOnMissingSubKey">Determines whether an exception should be thrown if the subkey does not exist.</param>
        void DeleteSubKeyTree(string path, bool throwOnMissingSubKey);

        /// <summary>
        /// Sets a value in the registry at the specified path and name. This method abstracts the process of writing values to the registry, allowing for easier testing and separation of concerns. The implementation can handle the details of how the registry is accessed, such as which root key to use or how to handle permissions. The method takes a path to the registry key, a name for the value, and the value itself, and it sets this value in the registry accordingly. This allows calling code to write values to the registry without needing to directly interact with the Windows Registry API, improving testability and maintainability.
        /// </summary>
        /// <param name="path">The path of the registry key where the value will be set.</param>
        /// <param name="name">The name of the value to set.</param>
        /// <param name="value">The value to set in the registry.</param>
        void SetValue(string path, string name, object value);
    }
}
