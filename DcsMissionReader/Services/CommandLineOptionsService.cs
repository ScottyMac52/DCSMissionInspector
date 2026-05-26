using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the ICommandLineOptionsService interface to parse command line arguments into an AppOptions object.
    /// This class is responsible for interpreting the command line switches and arguments, mapping them to the appropriate properties in the AppOptions model.
    /// </summary>
    public class CommandLineOptionsService : ICommandLineOptionsService
    {
        #region ICommandLineOptionsService Implementation

        public AppOptions Parse(string[] args)
        {
            // Convert to a HashSet for O(1) lookup performance
            var argSet = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);

            var options = new AppOptions
            {
                // Help flags
                ShowHelp = argSet.Contains("-h") || argSet.Contains("--help") || argSet.Contains("-?"),

                // Version flags
                ShowVersion = argSet.Contains("-v") || argSet.Contains("--ver") || argSet.Contains("--version"),

                // HTML
                CreateHtml = argSet.Contains("--html") || argSet.Contains("--create-html") || argSet.Contains("--out-html"),

                // JSON
                CreateJson = argSet.Contains("-j") || argSet.Contains("--json") || argSet.Contains("--out-json"),

                // Full
                FullExport = argSet.Contains("-f") || argSet.Contains("--full-export") || argSet.Contains("--full"),

                // KML
                CreateKml = argSet.Contains("-k") || argSet.Contains("--kml") || argSet.Contains("--google-earth"),

                // Registration
                CheckRegistration = argSet.Contains("-c") || argSet.Contains("--check") || argSet.Contains("--check-registration"),
                InstallRegistration = argSet.Contains("-i") || argSet.Contains("--install") || argSet.Contains("--install-registration"),
                UninstallRegistration = argSet.Contains("-u") || argSet.Contains("--uninstall") || argSet.Contains("--uninstall-registration"),

                // Files: Filter everything that doesn't start with '-' and ends in .miz
                MissionFiles = args.Where(a => !a.StartsWith('-') && a.EndsWith(".miz", StringComparison.OrdinalIgnoreCase)).ToList()
            };

            // Units handling: This remains the only part needing value-based logic
            // We check if the user passed --metric, --real, or --imperial
            if (argSet.Contains("--metric")) options.Units = UnitsSystem.Metric;
            else options.Units = UnitsSystem.Real;

            return options;
        }

        #endregion ICommandLineOptionsService Implementation
    }
}
