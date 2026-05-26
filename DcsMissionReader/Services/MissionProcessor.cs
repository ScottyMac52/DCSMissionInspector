using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using System.Text;
using System.Text.Json;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the IMissionProcessor interface to process DCS mission files based on the provided options. This class is responsible for reading the mission files, extracting relevant data, and generating output in various formats (HTML, JSON) as specified by the command line options. It handles the core logic of processing each mission file, including error handling and cleanup of temporary files.
    /// </summary>
    /// <remarks>
    /// Ctor
    /// </remarks>
    /// <param name="threatService">The threat database service used to retrieve threat ranges for units.</param>
    /// <param name="archiveService"> The mission archive service used to read mission files and resources.</param>
    /// <param name="converter">The coordinate converter service used to convert DCS coordinates to latitude and longitude.</param>
    /// <param name="strategies"> A collection of mission export strategies that determine how to export the processed mission data based on the provided options.</param>
    public class MissionProcessor(IThreatDatabaseService threatService, IMissionArchiveService archiveService, ICoordinateConverterService converter, IEnumerable<IMissionExportStrategy> strategies) : IMissionProcessor
    {
        #region IMissionProcessor implementation

        /// <summary>
        /// Processes the list of mission files provided in the AppOptions. For each mission file, it extracts the mission data, resolves any localized strings using the dictionary if available, and generates output based on the specified options (HTML report, JSON summary, full export). It also handles error cases such as missing mission files or extraction issues, and ensures that temporary files are cleaned up after processing.
        /// </summary>
        /// <param name="options"></param>
        public void Process(AppOptions options)
        {
            if (options.MissionFiles.Count == 0)
            {
                Console.WriteLine("Usage: DcsMissionReader <mission1.miz> [mission2.miz ...] [options]");
                Console.WriteLine("Options:");
                Console.WriteLine("  -h/--create-html   Create HTML briefing report");
                Console.WriteLine("  -j/--json          Create mission_summary.json");
                Console.WriteLine("  --full-export      Create mission_full.json (full raw data)");
                return;
            }

            bool anyExport = options.CreateHtml || options.CreateJson || options.FullExport;

            foreach (var mizPath in options.MissionFiles)
            {
                Console.WriteLine($"📦 Processing: {Path.GetFileName(mizPath)}");
                string missionData = archiveService.GetMissionContentAsync(mizPath).Result;

                // FIX: Pass mizPath into the method
                ProcessSingleMission(mizPath, missionData, options, anyExport);
                Console.WriteLine(new string('-', 80));
            }

            Console.WriteLine("✅ All done.");
        }

        #endregion IMissionProcessor implementation

        #region Private helper methods

        private void ProcessSingleMission(string mizPath, string missionContent, AppOptions options, bool anyExport)
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
                string mapName = File.Exists(Path.Combine(tempDir, "theatre"))
                    ? File.ReadAllText(Path.Combine(tempDir, "theatre")).Trim()
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
                if (anyExport || options.CreateKml)
                {
                    string cleanName = MissionUtils.SanitizeFileName(sortie);
                    string reportDir = Path.Combine(Directory.GetCurrentDirectory(), cleanName + "_Report");
                    Directory.CreateDirectory(reportDir);

                    // The 'strategies' collection is already injected via constructor
                    foreach (var strategy in strategies.Where(s => s.ShouldExport(options)))
                    {
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

        

        #endregion Private helper methods
    }
}
