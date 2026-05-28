using DcsMissionReader.Models;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for a service that processes DCS mission files based on the options provided. This service is responsible for taking the AppOptions object, which contains the command line options and the list of mission files to process, and performing the necessary operations to read the mission files and generate the desired output (HTML, JSON, etc.) based on those options.
    /// </summary>
    public interface IMissionProcessor
    {
        /// <summary>
        /// Processes the mission files asynchronously based on the provided options. This method serves the same purpose as the synchronous Process method but allows for asynchronous execution, which can be beneficial for performance when processing multiple mission files or when the processing involves I/O operations that can be awaited. The implementation of this method should ensure that it properly handles asynchronous operations and returns a Task that represents the ongoing processing work.
        /// </summary>
        /// <param name="options">The options that specify how the mission files should be processed.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task ProcessAsync(AppOptions options);
    }
}
