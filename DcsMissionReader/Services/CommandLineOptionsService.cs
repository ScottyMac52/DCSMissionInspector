using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the ICommandLineOptionsService interface to parse command line arguments into an AppOptions object.
    /// This class is responsible for interpreting the command line switches and arguments, mapping them to the appropriate properties in the AppOptions model.
    /// </summary>
    public class CommandLineOptionsService(IRegistryManagementService registryManagementService) : ICommandLineOptionsService
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

                // Post-briefing ACMI/KML generation
                PostBrief = argSet.Contains("--post-brief") || argSet.Contains("--post_brief") || argSet.Contains("--postbrief"),

                // Post-briefing ACMI file: Filter everything that doesn't start with '-' and ends in .acmi.zip
                PostBriefAcmiZipFilePath = args.Where(a => !a.StartsWith('-') && a.EndsWith(".zip.acmi", StringComparison.OrdinalIgnoreCase)).ToList().FirstOrDefault(),

                // Files: Filter everything that doesn't start with '-' and ends in .miz
                MissionFiles = args.Where(a => !a.StartsWith('-') && a.EndsWith(".miz", StringComparison.OrdinalIgnoreCase)).ToList()
            };

            // Units handling: This remains the only part needing value-based logic
            // We check if the user passed --metric, --real, or --imperial
            if (argSet.Contains("--metric")) options.Units = UnitsSystem.Metric;
            else options.Units = UnitsSystem.Real;

            return options;
        }

        public void PrintHelp()
        {
            // Retrieve version from the assembly for display
            var version = System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version?.ToString();

            Console.WriteLine($"DcsMissionReader v{version ?? "1.0.0.0"} - Usage: DcsMissionReader.exe [options] <files>");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  -h, -?, --help             Show this help menu.");
            Console.WriteLine("  -v, --ver, --version       Show application version.");
            Console.WriteLine("  --html, --create-html      Generate HTML report.");
            Console.WriteLine("  -j, --json                 Output mission data as JSON.");
            Console.WriteLine("  -f, --full, --full-export  Perform a full data export.");
            Console.WriteLine("  --metric, --real           Select output units (Metric or Imperial/Real).");
            Console.WriteLine("  -k, --kml, --google-earth  Generate KML files for mission routes.");
            Console.WriteLine("  --post-brief <file.zip>    Generate post-brief KML from zipped Tacview ACMI.");
            Console.WriteLine("  --post-brief-output <kml>  Optional output path for post-brief KML."); 
            Console.WriteLine("\nExample:");
            Console.WriteLine("  DcsMissionReader.exe --json --metric mission1.miz");
        }

        public void ShowVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            Console.WriteLine($"DcsMissionReader version {version ?? "1.0.0.0"}");
        }

        public void HandleRegistration(AppOptions options)
        {
            if (options.CheckRegistration)
            {
                Console.WriteLine("Checking registration...");
                registryManagementService.IsRegistered();
            }
            else if (options.InstallRegistration)
            {
                if (!registryManagementService.IsRegistered())
                {
                    Console.WriteLine("Installing registration...");
                    registryManagementService.Install();
                }
                 else
                {
                    Console.WriteLine("Registration is already installed.");
                }   
            }
            else if (options.UninstallRegistration)
            {
                if(!registryManagementService.IsRegistered())
                {
                    Console.WriteLine("Registration is not installed.");
                    return;
                }
                Console.WriteLine("Uninstalling registration...");
                registryManagementService.Uninstall();
            }
        }

        #endregion ICommandLineOptionsService Implementation
    }
}
