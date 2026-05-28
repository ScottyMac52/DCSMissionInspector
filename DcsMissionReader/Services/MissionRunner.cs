using DcsMissionReader.Models;
using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using System.Runtime;
using System.Threading.Tasks;

namespace DcsMissionReader
{
    public class MissionRunner
    {
        private readonly ICommandLineOptionsService _commandLineService;
        private readonly IMissionProcessor _missionProcessor;

        public MissionRunner(ICommandLineOptionsService commandLineService, IMissionProcessor missionProcessor)
        {
            _commandLineService = commandLineService;
            _missionProcessor = missionProcessor;
        }

        public async Task<int> RunAsync(string[] args)
        {
            // 1. Parse Arguments
            var options = _commandLineService.Parse(args);

            // 2. Validate
            if (options == null || options.HasErrors)
            {
                // The CommandLineService should handle printing the help text
                _commandLineService.PrintHelp();
                return -1;
            }

            // 3. Handle special options (version, help)
            if (options.ShowVersion || options.ShowHelp)
            {
                if(options.ShowVersion)
                {
                    _commandLineService.ShowVersion();
                    return 0;
                }   

                _commandLineService.PrintHelp();
                return 0; // Success exit code
            }

            if(options.InstallRegistration || options.UninstallRegistration || options.CheckRegistration)
            {
                _commandLineService.HandleRegistration(options);
                return 0; // Success exit code
            }

            // 5. Execute
            await _missionProcessor.ProcessAsync(options);

            return 0; // Success exit code
        }
    }
}