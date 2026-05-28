using DcsMissionReader.Models;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for a service that parses command line options for the application. This service is responsible for taking the command line arguments passed to the application and converting them into an AppOptions object that can be used by the rest of the application to determine how to process the mission files.
    /// </summary>
    public interface ICommandLineOptionsService
    {
        /// <summary>
        /// Handles the registration of the application, which may involve checking for existing registration, installing new registration, or uninstalling existing registration based on the options provided. This method should be called before parsing the command line arguments to ensure that any necessary registration actions are performed before the application proceeds with processing the mission files.
        /// </summary>
        /// <param name="options"></param>
        void HandleRegistration(AppOptions options);

        /// <summary>
        /// Parses the command line arguments and returns an AppOptions object containing the parsed options. If the arguments are invalid or if the user requests help, this method should handle printing the appropriate help text and return an AppOptions object that indicates an error state (e.g., HasErrors = true). The MissionRunner will then check for this error state and decide whether to proceed with processing or to exit gracefully.
        /// </summary>
        /// <param name="args">The command line arguments passed to the application.</param>
        /// <returns>An AppOptions object containing the parsed options. </returns>
        AppOptions Parse(string[] args);

        /// <summary>
        /// Prints help text to the console, providing usage instructions and information about the available command line options. This method should be called when the user requests help (e.g., by passing a "--help" flag) or when the provided arguments are invalid. The help text should be clear and concise, guiding the user on how to properly use the application and what options are available for processing DCS mission files.
        /// </summary>
        void PrintHelp();

        /// <summary>
        /// Shows the application version information to the console. This method should be called when the user requests version information (e.g., by passing a "--version" flag). The version information should be retrieved from the assembly metadata and displayed in a user-friendly format, allowing users to easily identify which version of the DCS Mission Reader they are using.
        /// </summary>
        void ShowVersion();
    }
}
