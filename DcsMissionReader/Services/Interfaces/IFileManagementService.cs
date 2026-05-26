namespace DcsMissionReader.Services.Interfaces
{
    /// <summary>
    /// Interface for file management operations related to DCS mission processing. This service is responsible for handling file operations such as copying images and kneeboards from the source mission directory to the target output directory. By abstracting these file operations into an interface, we can ensure that the file management logic is decoupled from the core mission processing logic, allowing for easier testing and maintenance of the code that relies on these file operations.
    /// </summary>
    public interface IFileManagementService
    {
        /// <summary>
        /// Copies image files from the source directory to the target directory. This method is responsible for handling the file operations necessary to ensure that all relevant images (e.g., mission thumbnails, kneeboard images) are copied from the original mission directory to the output directory where the processed mission data will be stored. The implementation of this method should handle any necessary checks for file existence, create target directories if they do not exist, and manage any exceptions that may arise during the file copying process. By centralizing this logic in a dedicated service, we can maintain a clean separation of concerns and improve the maintainability of the codebase.
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="targetDir"></param>
        void CopyImages(string sourceDir, string targetDir);

        /// <summary>
        /// Copies kneeboard files from the source directory to the target directory. This method is responsible for handling the file operations necessary to ensure that all relevant kneeboard files (e.g., PDF or image files used for in-game kneeboards) are copied from the original mission directory to the output directory where the processed mission data will be stored. The implementation of this method should handle any necessary checks for file existence, create target directories if they do not exist, and manage any exceptions that may arise during the file copying process. Additionally, this method returns an integer representing the number of kneeboard files that were successfully copied, allowing calling code to verify that the expected number of files were handled correctly. By centralizing this logic in a dedicated service, we can maintain a clean separation of concerns and improve the maintainability of the codebase.
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="targetDir"></param>
        /// <returns></returns>
        int CopyKneeboards(string sourceDir, string targetDir);
    }
}
