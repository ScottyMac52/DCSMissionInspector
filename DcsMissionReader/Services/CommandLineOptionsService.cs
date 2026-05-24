using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the ICommandLineOptionsService interface to parse command line arguments into an AppOptions object.
    /// This class is responsible for interpreting the command line switches and arguments, mapping them to the appropriate properties in the AppOptions model.
    /// </summary>
    public class CommandLineOptionsService : ICommandLineOptionsService
    {
        #region ICommandLineOptionsService Implementation

        /// <summary>
        /// Parses the command line arguments into an AppOptions object.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>An AppOptions object populated with the parsed values.</returns>
        public AppOptions Parse(string[] args)
        {
            var switchMappings = new Dictionary<string, string>
    {
        { "-h", "create-html" },
        { "--create-html", "create-html" },
        { "--html", "create-html" },
        { "-j", "json" },
        { "--json", "json" },
        { "--full-export", "full-export" },
        { "--full", "full-export" },
        { "--metric", "units" },
        { "--real", "units" },
        { "-k", "kml" },
        { "--kml", "kml" }
    };

            var config = new ConfigurationBuilder()
                .AddCommandLine(args, switchMappings)
                .Build();

            var options = new AppOptions
            {
                CreateHtml = config.GetValue<bool>("create-html"),
                CreateJson = config.GetValue<bool>("json"),
                FullExport = config.GetValue<bool>("full-export"),
                CreateKml = config.GetValue<bool>("kml"),
                MissionFiles = args
                    .Where(a => !a.StartsWith("-") && a.EndsWith(".miz", StringComparison.OrdinalIgnoreCase))
                    .ToList()
            };

            // Units handling
            string unitsStr = config["units"]?.ToLowerInvariant();
            options.Units = unitsStr == "metric" ? UnitsSystem.Metric : UnitsSystem.Real;

            return options;
        }

        #endregion ICommandLineOptionsService Implementation
    }
}
