using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services
{
    public class RegistryManagementService(IRegistryWrapper? registryWrapper = null, IIdentityService? identityService = null) : IRegistryManagementService
    {
        private const string BaseKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell";
        private readonly string[] _commands = { "DCS.HTML", "DCS.KML", "DCS.JSON" };
        private readonly IRegistryWrapper _registryWrapper = registryWrapper ?? new RegistryWrapper();
        private readonly IIdentityService _identityService = identityService ?? new IdentityService();

        public bool IsAdministrator()
        {
            return _identityService.IsAdministrator();
        }

        public bool IsRegistered()
        {
            // Check if the first command exists to verify installation 
            using var key = _registryWrapper.OpenSubKey($@"{BaseKey}\{_commands[0]}");
            return key != null;
        }

        public void Install()
        {
            if(!IsAdministrator())
            {
                throw new InvalidOperationException("Administrator privileges are required to install registry entries.");
            }

            // 1. Create the Parent Menu in HKEY_CLASSES_ROOT
            using (var parentKey = _registryWrapper.CreateSubKey(@"miz_auto_file\shell\DCS"))
            {
                _registryWrapper.SetValue(@"miz_auto_file\shell\DCS", null, "DCS Mission Tools");
                _registryWrapper.SetValue(@"miz_auto_file\shell\DCS", "SubCommands", "DCS.HTML;DCS.KML;DCS.JSON");
            }

            // 2. Create individual commands in CommandStore 
            CreateCommand("DCS.HTML", "Create HTML", "\"D:\\DcsMissionReader\\DcsMissionReader.exe\" \"%1\" --html true");
            CreateCommand("DCS.KML", "Export KML", "\"D:\\DcsMissionReader\\DcsMissionReader.exe\" \"%1\" --kml true");
            CreateCommand("DCS.JSON", "Export JSON", "\"D:\\DcsMissionReader\\DcsMissionReader.exe\" \"%1\" --json true");
        }

        public void Uninstall()
        {
            if (!IsAdministrator())
            {
                throw new InvalidOperationException("Administrator privileges are required to uninstall registry entries.");
            }

            // Remove Parent Menu
            _registryWrapper.DeleteSubKeyTree(@"miz_auto_file\shell\DCS", false);

            // Remove Commands
            foreach (var cmd in _commands)
            {
                _registryWrapper.DeleteSubKeyTree($@"{BaseKey}\{cmd}", false);
            }
        }

        private void CreateCommand(string name, string displayName, string commandPath)
        {
            // Create the command key and set the display name
            _registryWrapper.SetValue($@"{BaseKey}\{name}", null, displayName);

            // Create the sub-key "command" and set the command path
            _registryWrapper.SetValue($@"{BaseKey}\{name}\command", null, commandPath);
        }
    }
}

