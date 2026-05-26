using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DcsMissionReader
{
    class Program
    {
        /// <summary>
        /// The main entry point of the application.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            // Register our injectable services
            services.AddSingleton<ICommandLineOptionsService, CommandLineOptionsService>();
            services.AddSingleton<IMissionProcessor, MissionProcessor>();
            services.AddSingleton<IThreatDatabaseService, JsonThreatDatabaseService>();
            services.AddSingleton<IMissionArchiveService, MissionArchiveService>();
            services.AddSingleton<ICoordinateConverterService, CoordinateConverterService>();
            services.AddSingleton<IRegistryManagementService, RegistryManagementService>();

            using var serviceProvider = services.BuildServiceProvider();

            var cliService = serviceProvider.GetRequiredService<ICommandLineOptionsService>();
            var options = cliService.Parse(args);

            var registryService = serviceProvider.GetRequiredService<IRegistryManagementService>();

            if (options.CheckRegistration)
            {
                bool isInstalled = registryService.IsRegistered();
                Console.WriteLine(isInstalled ? "Registration found." : "Registration not found.");
                return; // Exit here so processing doesn't start    
            }

            if (options.InstallRegistration)
            {
                if(registryService.IsRegistered())
                {
                    Console.WriteLine("Registration is already installed.");
                    return; // Exit here so processing doesn't start
                }

                registryService.Install();
                Console.WriteLine("Registration installed successfully.");
                return; // Exit here so processing doesn't start
            }

            if (options.ShowVersion)
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString();

                Console.WriteLine($"DcsMissionReader version {version ?? "1.0.0.0"}");
                return; // Exit here so processing doesn't start
            }

            if(options.ShowHelp)
            {
                PrintHelp();
                return; // Exit here so processing doesn't start
            }

            var processor = serviceProvider.GetRequiredService<IMissionProcessor>();
            processor.Process(options);
        }

        static void PrintHelp()
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
            Console.WriteLine("\nExample:");
            Console.WriteLine("  DcsMissionReader.exe --json --metric mission1.miz");
        }

// ... existing logic ...
    }
}
