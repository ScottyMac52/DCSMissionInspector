using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the IMissionProcessor interface to process DCS mission files based on the provided options. This class is responsible for reading the mission files, extracting relevant data, and generating output in various formats (HTML, JSON) as specified by the command line options. It handles the core logic of processing each mission file, including error handling and cleanup of temporary files.
    /// </summary>
    /// <remarks>
    /// Ctor
    /// </remarks>
    /// <param name="archiveService"> The mission archive service used to read mission files and resources.</param>
    /// <param name="strategies"> A collection of mission export strategies that determine how to export the processed mission data based on the provided options.</param>
    public class MissionProcessor(IMissionArchiveService archiveService, IEnumerable<IMissionExportStrategy> strategies) : IMissionProcessor
    {
        #region IMissionProcessor implementation

        public async Task ProcessAsync(AppOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (options.PostBrief)
            {
                ProcessPostBriefing(options);
                Console.WriteLine("✅ All done.");
                return;
            }

            bool anyExport = options.MissionFiles.Count > 0 &&
                (options.CreateHtml || options.CreateJson || options.FullExport);

            foreach (var mizPath in options.MissionFiles)
            {
                Console.WriteLine($"📦 Processing: {Path.GetFileName(mizPath)}");

                string missionData = await archiveService.GetMissionContentAsync(mizPath);

                await ProcessSingleMissionAsync(mizPath, missionData, options, anyExport);

                Console.WriteLine(new string('-', 80));
            }

            Console.WriteLine("✅ All done.");
        }

        #endregion IMissionProcessor implementation

        #region Private helper methods

        private void ProcessPostBriefing(AppOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.PostBriefAcmiZipFilePath))
            {
                throw new InvalidOperationException(
                    "--post-brief requires a zipped Tacview ACMI file path.");
            }

            if (!File.Exists(options.PostBriefAcmiZipFilePath))
            {
                throw new FileNotFoundException(
                    "Post-briefing ACMI zip file was not found.",
                    options.PostBriefAcmiZipFilePath);
            }

            string sortie = GetPostBriefSortieName(options.PostBriefAcmiZipFilePath);
            string cleanName = MissionUtils.SanitizeFileName(sortie);

            string reportDir = Path.Combine(
                Directory.GetCurrentDirectory(),
                $"{cleanName}_PostBrief_Report");

            Directory.CreateDirectory(reportDir);

            var context = new MissionContext
            {
                MizPath = options.PostBriefAcmiZipFilePath,
                Sortie = sortie,
                ReportDir = reportDir,
                TempDir = string.Empty,
                Theatre = string.Empty,
                Options = options
            };

            foreach (var strategy in strategies.Where(s => s.ShouldExport(options)))
            {
                strategy.Export(context);
            }

            Console.WriteLine($"📁 Post-brief exports complete → {context.ReportDir}");
        }

        /// <summary>
        /// Processes a single mission file by extracting relevant data, resolving metadata, and delegating export tasks to the appropriate strategies based on the provided options. This method handles the core logic of reading the mission content, parsing it using MoonSharp, and generating output in various formats as specified by the command line options. It also includes error handling and cleanup of temporary files created during processing.   
        /// </summary>
        /// <param name="mizPath">Path to the mission file. </param>
        /// <param name="missionContent">The content of the mission file.</param>
        /// <param name="options">The options that specify how the mission files should be processed.</param>
        /// <param name="anyExport">Indicates whether any export operation is requested.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private async Task ProcessSingleMissionAsync(string mizPath, string missionContent, AppOptions options, bool anyExport)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DCSMission_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. Data Setup & Parsing
                var script = new Script();
                script.DoString(missionContent);
                Table mission = script.Globals.Get("mission").Table;
                string theatre = mission.Get("theatre")?.String ?? "caucasus";

                Table? dictTable = null;
                string dictPath = Path.Combine(tempDir, @"l10n\DEFAULT\dictionary");
                if (File.Exists(dictPath))
                {
                    script.DoFile(dictPath);
                    var dictVal = script.Globals.Get("dictionary");
                    if (dictVal.Type == DataType.Table) dictTable = dictVal.Table;
                }

                // 2. Resolve Metadata
                // FIX: Use async file reading for the theatre file
                string mapName = File.Exists(Path.Combine(tempDir, "theatre"))
                    ? (await File.ReadAllTextAsync(Path.Combine(tempDir, "theatre"))).Trim()
                    : "Unknown";

                string sortieRaw = MissionUtils.Resolve(mission.Get("sortie"), dictTable);
                string sortie = (!string.IsNullOrWhiteSpace(sortieRaw) && !sortieRaw.StartsWith("DictKey_"))
                    ? sortieRaw
                    : Path.GetFileNameWithoutExtension(mizPath);

                string description = MissionUtils.Resolve(mission.Get("descriptionText"), dictTable);

                // 3. Output Header
                Console.WriteLine($"✅ Mission loaded: {sortie} | Map: {mapName}");
                Console.WriteLine("   Briefing: " + (string.IsNullOrWhiteSpace(description) ? "(none)" : description.Substring(0, Math.Min(description.Length, 100)) + "..."));

                // Construct the Context
                var context = new MissionContext
                {
                    MizPath = mizPath,
                    MissionTable = mission,
                    DictTable = dictTable,
                    Theatre = theatre,
                    Sortie = sortie,
                    TempDir = tempDir,
                    ReportDir = Path.Combine(Directory.GetCurrentDirectory(), MissionUtils.SanitizeFileName(sortie) + "_Report"),
                    Options = options
                };

                // 4. Delegation (Orchestration)
                if (anyExport || options.CreateKml || options.PostBrief)
                {
                    string cleanName = MissionUtils.SanitizeFileName(sortie);
                    string reportDir = Path.Combine(Directory.GetCurrentDirectory(), cleanName + "_Report");
                    Directory.CreateDirectory(reportDir);

                    foreach (var strategy in strategies.Where(s => s.ShouldExport(options)))
                    {
                        // Note: If your IMissionExportStrategy ever implements async file writing, 
                        // this loop would become: await strategy.ExportAsync(context);
                        strategy.Export(context);
                    }

                    Console.WriteLine($"📁 Exports complete → {context.ReportDir}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing {Path.GetFileName(mizPath)}: {ex.Message}");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        private static string GetPostBriefSortieName(string acmiZipFilePath)
        {
            string fileName = Path.GetFileName(acmiZipFilePath);

            if (fileName.EndsWith(".acmi.zip", StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^".acmi.zip".Length];
            }

            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^".zip".Length];
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        #endregion Private helper methods
    }
}
