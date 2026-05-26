using DcsMissionReader.Services.Interfaces;
using Microsoft.Win32;
using System.Diagnostics.CodeAnalysis;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the IRegistryWrapper interface to provide a concrete implementation for interacting with the Windows Registry. This class abstracts away direct interactions with the Windows Registry, allowing for easier testing and separation of concerns. The methods in this class handle opening, creating, and deleting registry subkeys, as well as setting values in the registry. By using this wrapper, other parts of the application can interact with the registry without needing to directly access the Windows Registry API, improving testability and maintainability of the code that relies on registry operations.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public class RegistryWrapper : IRegistryWrapper
    {
        /// <inheritdoc/>
        public RegistryKey? OpenSubKey(string path) => Registry.LocalMachine.OpenSubKey(path);

        /// <inheritdoc/>
        public RegistryKey CreateSubKey(string path) => Registry.LocalMachine.CreateSubKey(path);

        /// <inheritdoc/>
        public void DeleteSubKeyTree(string path, bool throwOnMissingSubKey) =>
            Registry.LocalMachine.DeleteSubKeyTree(path, throwOnMissingSubKey);

        /// <inheritdoc/>
        public void SetValue(string path, string name, object value)
        {
            using var key = Registry.LocalMachine.CreateSubKey(path);
            key.SetValue(name, value);
        }
    }
}
