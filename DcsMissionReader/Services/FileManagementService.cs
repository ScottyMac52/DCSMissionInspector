using DcsMissionReader.Services.Interfaces;
using System.IO.Abstractions;

namespace DcsMissionReader.Services
{
    public class FileManagementService(IFileSystem fileSystem) : IFileManagementService
    {
        /// <summary>
        /// Copies kneeboard images from the temporary directory to the report's kneeboards directory. It searches for any folders named "Kneeboard" (case-insensitive) within the extracted mission files and copies all image files (with extensions .jpg, .jpeg, .png, .pdf) while preserving the subfolder structure. The method returns the total count of kneeboard pages copied, which can be used for reporting purposes in the generated HTML and JSON outputs.    
        /// </summary>
        /// <param name="tempDir">The temporary directory containing the extracted mission files.</param>
        /// <param name="kneeboardsDir">The destination directory for the kneeboard images.</param>
        /// <returns>The total count of kneeboard pages copied.</returns>
        public int CopyKneeboards(string tempDir, string kneeboardsDir)
        {
            string[] kneeboardExts = { ".jpg", ".jpeg", ".png", ".pdf" };
            var kneeboardFolders = fileSystem.Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories)
                .Where(d => fileSystem.Path.GetFileName(d).Equals("Kneeboard", StringComparison.OrdinalIgnoreCase) ||
                            fileSystem.Path.GetFileName(d).Equals("KNEEBOARD", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int count = 0;
            foreach (var kbFolder in kneeboardFolders)
            {
                var files = fileSystem.Directory.GetFiles(kbFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => kneeboardExts.Contains(Path.GetExtension(f).ToLowerInvariant()));

                foreach (var src in files)
                {
                    // Preserve subfolder structure (e.g. Kneeboard/IMAGES/F-16C/...)
                    string relative = fileSystem.Path.GetRelativePath(kbFolder, src);
                    string dest = fileSystem.Path.Combine(kneeboardsDir, relative);
                    fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    fileSystem.File.Copy(src, dest, overwrite: true);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Copies image files from the temporary directory to the report's images directory.
        /// </summary>
        /// <param name="tempDir">The temporary directory containing the extracted mission files.</param>
        /// <param name="imagesDir">The target directory for the images.</param>
        public void CopyImages(string tempDir, string imagesDir)
        {
            string[] imageExts = { ".jpg", ".jpeg", ".png", ".dds" };
            var images = fileSystem.Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                .Where(f => imageExts.Contains(fileSystem.Path.GetExtension(f).ToLowerInvariant()));

            foreach (var src in images)
            {
                string dest = fileSystem.Path.Combine(imagesDir, fileSystem.Path.GetFileName(src));
                fileSystem.File.Copy(src, dest, overwrite: true);
            }
        }


    }
}
