using DcsMissionReader.Models;
using MoonSharp.Interpreter;

namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for mission export strategies, defining the contract for exporting mission data in various formats. This interface allows for different implementations that can handle specific export logic based on the provided AppOptions. Each implementation can determine whether it should run based on the options and then execute the export logic accordingly. This design promotes separation of concerns and makes it easier to add new export formats in the future without modifying existing code, adhering to the Open/Closed Principle of software design.
    /// </summary>
    public interface IMissionExportStrategy
    {
        /// <summary>
        /// Determines whether this export strategy should be executed based on the provided application options. This method allows each implementation to check the relevant options and decide if it is responsible for handling the export process. For example, an implementation that exports to HTML might check if the CreateHtml option is set to true, while another implementation for JSON might check the CreateJson option. This approach ensures that only the appropriate export logic is executed based on user preferences or command-line arguments.
        /// </summary>
        /// <param name="options">The application options to evaluate.</param>
        /// <returns>True if this export strategy should be executed; otherwise, false.</returns>
        bool ShouldExport(AppOptions options);

        /// <summary>
        /// Executes the specific export logic for this strategy.
        /// </summary>
        /// <param name="context">The mission context containing all necessary information for the export.</param>
        void Export(MissionContext context);
    }
}
