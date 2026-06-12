using DcsMissionReader.Services;
using DcsMissionReader.Services.Generators;
using DcsMissionReader.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Abstractions;
using DcsMissionReader.Models;
using Microsoft.Extensions.Configuration;

namespace DcsMissionReader
{
    class Program
    {

        public static async Task<int> Main(string[] args)
        {
            // 1. Setup DI Container
            var serviceProvider = ConfigureServices();

            // 2. Resolve the Runner
            var runner = serviceProvider.GetRequiredService<MissionRunner>();

            // 3. Execute with the options
            return await runner.RunAsync(args);
        }

        private static ServiceProvider ConfigureServices()
        {
            // Create the DI container and register services. The services are registered as singletons because they do not hold any state that would require multiple instances, and it allows for efficient reuse throughout the application's lifetime. This also simplifies dependency management and ensures consistent behavior across the application when processing missions, managing files, and handling registry entries.
            var services = new ServiceCollection();

            // Configuration is registered as a singleton because it is typically read once at the start of the application and then used throughout the application's lifetime. By registering it as a singleton, we ensure that there is only one instance of the configuration throughout the application, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when accessing configuration settings during mission processing and other operations.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            services.AddSingleton(configuration);

            // Command line options service should be a singleton since we only need to parse the options once and it doesn't hold any state that would require multiple instances. The same applies to the MissionProcessor and other services that are designed to be stateless or hold shared resources.
            services.AddSingleton<ICommandLineOptionsService, CommandLineOptionsService>();

            // MissionProcessor is the core service that orchestrates the processing of mission files. It depends on other services to perform specific tasks, but it itself does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of MissionProcessor throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when processing missions.
            services.AddSingleton<IMissionProcessor, MissionProcessor>();

            // ThreatDatabaseService is responsible for providing threat range information for units, and it does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the ThreatDatabaseService throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when retrieving threat range information for units during mission processing.
            services.AddSingleton<IThreatDatabaseService, JsonThreatDatabaseService>();

            // MissionArchiveService is responsible for reading mission files and resources from a .miz archive, and it does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the MissionArchiveService throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when reading mission files during processing.
            services.AddSingleton<IMissionArchiveService, MissionArchiveService>();

            // CoordinateConverterService is responsible for converting DCS coordinates to latitude and longitude, and it does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the CoordinateConverterService throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when converting coordinates during mission processing.
            services.AddSingleton<ICoordinateConverterService, CoordinateConverterService>();

            // RegistryManagementService is responsible for managing the Windows registry entries for handling DCS mission files, and it does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the RegistryManagementService throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when managing registry entries for DCS mission files.
            services.AddSingleton<IRegistryManagementService, RegistryManagementService>();

            // HtmlReportGenerator, JsonSummaryGenerator, KmlExportGenerator, and PostBriefingExportGenerator are all implementations of the IMissionExportStrategy interface, and they do not hold any state that would require multiple instances. By registering them as singletons, we ensure that there is only one instance of each export strategy throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when exporting mission data in various formats during processing.
            services.AddSingleton<IMissionExportStrategy, HtmlReportGenerator>();
            services.AddSingleton<IMissionExportStrategy, JsonSummaryGenerator>();
            services.AddSingleton<IMissionExportStrategy, KmlExportGenerator>();
            services.AddSingleton<IMissionExportStrategy, PostBriefingExportGenerator>();

            // FileManagementService and FileSystem are responsible for managing file operations and abstracting file system interactions, respectively. They do not hold any state that would require multiple instances. By registering them as singletons, we ensure that there is only one instance of each service throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when performing file operations and interacting with the file system during mission processing.
            services.AddSingleton<IFileManagementService, FileManagementService>();
            services.AddSingleton<IFileSystem, FileSystem>();

            // WeaponDatabaseService is responsible for providing weapon information and does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the WeaponDatabaseService throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when retrieving weapon information during mission processing.
            services.AddSingleton<IWeaponDatabaseService, JsonWeaponDatabaseService>();

            // PostBriefingService is responsible for creating KML files from ACMI data for post-briefing analysis, and it does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the PostBriefingService throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when creating KML files for post-briefing analysis during mission processing.
            services.AddSingleton<IPostBriefingService, PostBriefingService>();

            // BriefingStylesService is responsible for providing styling information for briefing generation, and it does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the BriefingStylesService throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when providing styling information for briefing generation during mission processing.
            services.AddSingleton<IBriefingStylesService, BriefingStylesService>();

            // MissionRunner is the main service that orchestrates the execution of the application, and it does not hold any state that would require multiple instances. By registering it as a singleton, we ensure that there is only one instance of the MissionRunner throughout the application's lifetime, which can be efficiently reused whenever needed. This also simplifies dependency management and ensures consistent behavior across the application when running the main execution flow for processing missions based on the provided command line options.
            services.AddSingleton<MissionRunner>();
            return services.BuildServiceProvider();
        }
    }
}
