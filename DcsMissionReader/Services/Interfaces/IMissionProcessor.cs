using DcsMissionReader.Models;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for a service that processes DCS mission files based on the options provided. This service is responsible for taking the AppOptions object, which contains the command line options and the list of mission files to process, and performing the necessary operations to read the mission files and generate the desired output (HTML, JSON, etc.) based on those options.
    /// </summary>
    public interface IMissionProcessor
    {
        /// <summary>
        /// Processes the mission files based on the provided options. This method will read the mission files specified in the AppOptions, and depending on the options set (such as CreateHtml, CreateJson, FullExport), it will generate the appropriate output for each mission file. The processing may involve parsing the mission files, extracting relevant data, and then formatting that data into the desired output formats.
        /// </summary>
        /// <param name="options">The options that specify how the mission files should be processed.</param>
        void Process(AppOptions options);
    }
}
