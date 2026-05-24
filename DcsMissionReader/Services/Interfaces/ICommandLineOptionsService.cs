using DcsMissionReader.Models;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for a service that parses command line options for the application. This service is responsible for taking the command line arguments passed to the application and converting them into an AppOptions object that can be used by the rest of the application to determine how to process the mission files.
    /// </summary>
    public interface ICommandLineOptionsService
    {
        AppOptions Parse(string[] args);
    }
}
