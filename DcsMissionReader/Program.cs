using DcsMissionReader.Services;
using DcsMissionReader.Services.Generators;
using DcsMissionReader.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Abstractions;

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
            var services = new ServiceCollection();

            // Register our injectable services
            services.AddSingleton<ICommandLineOptionsService, CommandLineOptionsService>();
            services.AddSingleton<IMissionProcessor, MissionProcessor>();
            services.AddSingleton<IThreatDatabaseService, JsonThreatDatabaseService>();
            services.AddSingleton<IMissionArchiveService, MissionArchiveService>();
            services.AddSingleton<ICoordinateConverterService, CoordinateConverterService>();
            services.AddSingleton<IRegistryManagementService, RegistryManagementService>();
            // Register each strategy individually
            services.AddSingleton<IMissionExportStrategy, HtmlReportGenerator>();
            services.AddSingleton<IMissionExportStrategy, JsonSummaryGenerator>();
            services.AddSingleton<IMissionExportStrategy, KmlExportGenerator>();
            services.AddSingleton<IFileManagementService, FileManagementService>();
            services.AddSingleton<IFileSystem, FileSystem>();

            // Register your new runner
            services.AddSingleton<MissionRunner>();
            return services.BuildServiceProvider();
        }
    }
}
