using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddSingleton<IDcsDatabaseParserService, DcsDatabaseParserService>();

            using var serviceProvider = services.BuildServiceProvider();

            var cliService = serviceProvider.GetRequiredService<ICommandLineOptionsService>();
            var options = cliService.Parse(args);

            var processor = serviceProvider.GetRequiredService<IMissionProcessor>();
            processor.Process(options);
        }
    }
}
