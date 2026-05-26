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
    public class MissionProcessor(IThreatDatabaseService threatService, IMissionArchiveService archiveService, ICoordinateConverterService converter) : IMissionProcessor
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

        /// <summary>
        /// Processes a single mission file (.miz). It extracts the mission data, resolves localized strings, and generates output based on the specified options. The method handles the entire lifecycle of processing a mission file, including error handling and cleanup of temporary files.
        /// </summary>
        /// <param name="mizPath">The file path of the mission file being processed, used for naming outputs and error messages.</param>
        /// <param name="missionContent">The content of the mission file (.miz) to process.</param>
        /// <param name="options">The application options specifying what outputs to generate.</param>
        /// <param name="anyExport">Indicates whether any export option is enabled.</param>
        private void ProcessSingleMission(string mizPath, string missionContent, AppOptions options, bool anyExport)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DCSMission_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var script = new Script();
                script.DoString(missionContent);
                Table mission = script.Globals.Get("mission").Table;
                string theatre = mission.Get("theatre")?.String ?? "caucasus";

                Table dictTable = null;
                string dictPath = Path.Combine(tempDir, @"l10n\DEFAULT\dictionary");
                if (File.Exists(dictPath))
                {
                    script.DoFile(dictPath);
                    var dictVal = script.Globals.Get("dictionary");
                    if (dictVal.Type == DataType.Table) dictTable = dictVal.Table;
                }

                string mapName = File.Exists(Path.Combine(tempDir, "theatre"))
                    ? File.ReadAllText(Path.Combine(tempDir, "theatre")).Trim()
                    : "Unknown";

                // Improved name resolution with fallback
                string sortieRaw = Resolve(mission.Get("sortie"), dictTable);
                string sortie = (!string.IsNullOrWhiteSpace(sortieRaw) && !sortieRaw.StartsWith("DictKey_"))
                    ? sortieRaw
                    : Path.GetFileNameWithoutExtension(mizPath);

                string description = Resolve(mission.Get("descriptionText"), dictTable);
                string blueTask = Resolve(mission.Get("descriptionBlueTask"), dictTable);
                string redTask = Resolve(mission.Get("descriptionRedTask"), dictTable);

                var dateTable = mission.Get("date").Table;
                string fullDate = $"{(int)dateTable.Get("Year").Number}-{(int)dateTable.Get("Month").Number:D2}-{(int)dateTable.Get("Day").Number:D2}";
                double startSec = mission.Get("start_time").Number;
                string startTime = $"{(int)(startSec / 3600):D2}:{(int)((startSec % 3600) / 60):D2}";
                string version = mission.Get("version").ToString();

                // Console output
                Console.WriteLine($"✅ Mission loaded successfully!");
                Console.WriteLine($"   Name          : {sortie}");
                Console.WriteLine($"   Map           : {mapName}");
                Console.WriteLine($"   Date          : {fullDate}");
                Console.WriteLine($"   Start Time    : {startTime}");
                Console.WriteLine($"   Version       : {version}");
                Console.WriteLine("   Briefing: " + (string.IsNullOrWhiteSpace(description) ? "(none)" : description));

                if (anyExport || options.CreateKml)
                {
                    string cleanName = SanitizeFileName(sortie);
                    string reportDir = Path.Combine(Directory.GetCurrentDirectory(), cleanName + "_Report");
                    Directory.CreateDirectory(reportDir);

                    string imagesDir = Path.Combine(reportDir, "images");
                    Directory.CreateDirectory(imagesDir);
                    CopyImages(tempDir, imagesDir);

                    string kneeboardsDir = Path.Combine(reportDir, "kneeboards");
                    Directory.CreateDirectory(kneeboardsDir);
                    int kneeboardCount = CopyKneeboards(tempDir, kneeboardsDir);

                    if (options.CreateHtml)
                        GenerateHtmlReport(reportDir, imagesDir, kneeboardsDir, sortie, mapName, fullDate, startTime, version, description, blueTask, redTask, kneeboardCount, mission, options);

                    if (options.CreateJson)
                        GenerateJsonSummary(reportDir, sortie, mapName, fullDate, startTime, version, description, blueTask, redTask, imagesDir, kneeboardsDir);

                    if (options.FullExport)
                        GenerateFullExport(reportDir, mission, dictTable, tempDir);

                    if (options.CreateKml)
                        GenerateKmlExport(reportDir, sortie, mission, theatre);

                    Console.WriteLine($"📁 Report folder created → {reportDir}");
                    if (kneeboardCount > 0)
                        Console.WriteLine($"   📋 {kneeboardCount} custom kneeboard page(s) extracted");
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

        /// <summary>
        /// Copies kneeboard images from the temporary directory to the report's kneeboards directory. It searches for any folders named "Kneeboard" (case-insensitive) within the extracted mission files and copies all image files (with extensions .jpg, .jpeg, .png, .pdf) while preserving the subfolder structure. The method returns the total count of kneeboard pages copied, which can be used for reporting purposes in the generated HTML and JSON outputs.    
        /// </summary>
        /// <param name="tempDir">The temporary directory containing the extracted mission files.</param>
        /// <param name="kneeboardsDir">The destination directory for the kneeboard images.</param>
        /// <returns>The total count of kneeboard pages copied.</returns>
        private static int CopyKneeboards(string tempDir, string kneeboardsDir)
        {
            string[] kneeboardExts = { ".jpg", ".jpeg", ".png", ".pdf" };
            var kneeboardFolders = Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories)
                .Where(d => Path.GetFileName(d).Equals("Kneeboard", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(d).Equals("KNEEBOARD", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int count = 0;
            foreach (var kbFolder in kneeboardFolders)
            {
                var files = Directory.GetFiles(kbFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => kneeboardExts.Contains(Path.GetExtension(f).ToLowerInvariant()));

                foreach (var src in files)
                {
                    // Preserve subfolder structure (e.g. Kneeboard/IMAGES/F-16C/...)
                    string relative = Path.GetRelativePath(kbFolder, src);
                    string dest = Path.Combine(kneeboardsDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(src, dest, overwrite: true);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Resolves a DynValue that may be a localized string reference (e.g., "DictKey_12345") using the provided dictionary table. If the value is a string that starts with "DictKey_" and the dictionary table is available, it looks up the corresponding string in the dictionary. If the value is not a string or does not start with "DictKey_", it returns the value as a string directly. This method ensures that any localized strings in the mission data are properly resolved for display and reporting purposes.
        /// </summary>
        /// <param name="val">The DynValue to resolve.</param>
        /// <param name="dictTable">The dictionary table used for resolving localized strings.</param>
        /// <returns>The resolved string.</returns>
        private static string Resolve(DynValue val, Table dictTable)
        {
            if (val.Type != DataType.String) return val.ToString() ?? string.Empty;
            string text = val.String;
            if (text.StartsWith("DictKey_") && dictTable != null)
            {
                var resolved = dictTable.Get(text);
                if (resolved.Type == DataType.String) return resolved.String;
            }
            return text;
        }

        /// <summary>
        /// Copies image files from the temporary directory to the report's images directory.
        /// </summary>
        /// <param name="tempDir">The temporary directory containing the extracted mission files.</param>
        /// <param name="imagesDir">The target directory for the images.</param>
        private static void CopyImages(string tempDir, string imagesDir)
        {
            string[] imageExts = { ".jpg", ".jpeg", ".png", ".dds" };
            var images = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                .Where(f => imageExts.Contains(Path.GetExtension(f).ToLowerInvariant()));

            foreach (var src in images)
            {
                string dest = Path.Combine(imagesDir, Path.GetFileName(src));
                File.Copy(src, dest, overwrite: true);
            }
        }

        /// <summary>
        /// Generates an HTML report for the mission briefing. The report includes the mission name, map, date, start time, version, briefing text, blue and red tasks (if available), and a gallery of briefing images. The HTML is styled using Tailwind CSS for a clean and modern look. The generated report is saved as "index.html" in the specified report directory, and the images are copied to an "images" subdirectory for display in the report.  
        /// </summary>
        /// <param name="reportDir">The directory where the report will be saved.</param>
        /// <param name="imagesDir">The directory where the images will be saved.</param>
        /// <param name="sortie">The mission sortie name.</param>
        /// <param name="mapName">The name of the map.</param>
        /// <param name="fullDate">The full date of the mission.</param>
        /// <param name="startTime">The start time of the mission.</param>
        /// <param name="version">The version of the mission.</param>
        /// <param name="description">The briefing text of the mission.</param>
        /// <param name="blueTask">The blue task description.</param>
        /// <param name="redTask">The red task description.</param>
        /// <param name="kneeboardsDir"> The directory where custom kneeboards are saved (for counting purposes).</param>
        /// <param name="kneeboardCount">The number of custom kneeboards.</param>
        /// <param name="mission">The mission table (for generating the ATO section).</param>
        /// <param name="options">The application options (for generating the weather section).</param>
        private void GenerateHtmlReport(string reportDir, string imagesDir, string kneeboardsDir, string sortie,
            string mapName, string fullDate, string startTime, string version, string description,
            string blueTask, string redTask, int kneeboardCount, Table mission, AppOptions options)
        {
            string htmlPath = Path.Combine(reportDir, "index.html");

            // Kneeboard grid (unchanged)
            string kneeboardHtml = "";
            if (kneeboardCount > 0)
            {
                var kbFiles = Directory.GetFiles(kneeboardsDir, "*.*", SearchOption.AllDirectories)
                    .OrderBy(f => f)
                    .ToList();

                kneeboardHtml = $@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">📋 Custom Kneeboards ({kneeboardCount} pages)</h2>
<div class=""image-grid"">";

                foreach (var f in kbFiles)
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    string relativePath = Path.GetRelativePath(kneeboardsDir, f);
                    string preview = ext == ".pdf"
                        ? @"<div class=""h-64 flex items-center justify-center bg-gray-800 text-4xl"">📄</div>"
                        : $@"<img src=""kneeboards/{relativePath}"" class=""w-full"">";

                    kneeboardHtml += $@"<div class=""bg-gray-900 rounded-xl overflow-hidden border border-gray-700"">
    {preview}
    <div class=""px-4 py-2 text-xs text-gray-400 font-mono"">{relativePath}</div>
</div>";
                }
                kneeboardHtml += "</div>";
            }

            string html = $@"<!DOCTYPE html>
                <html lang=""en""><head><meta charset=""utf-8""><title>{sortie}</title>
                <script src=""https://cdn.tailwindcss.com""></script>
                <style>body{{font-family:system-ui,sans-serif;}}.image-grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(400px,1fr));gap:1rem;}} table{{border-collapse:collapse;}} th,td{{padding:0.75rem;}}</style>
                </head>
                <body class=""bg-gray-950 text-gray-100"">
                <div class=""max-w-5xl mx-auto p-8"">
                <h1 class=""text-5xl font-bold mb-2"">{sortie}</h1>
                <div class=""flex gap-6 text-sm text-gray-400 mb-8"">
                <div><span class=""font-semibold"">Map:</span> {mapName}</div>
                <div><span class=""font-semibold"">Date:</span> {fullDate}</div>
                <div><span class=""font-semibold"">Start:</span> {startTime}</div>
                <div><span class=""font-semibold"">Version:</span> {version}</div>
                </div>

                <h2 class=""text-2xl font-semibold mb-4 border-b border-gray-700 pb-2"">Briefing</h2>
                <div class=""prose prose-invert max-w-none text-lg"">{(string.IsNullOrWhiteSpace(description) ? "<p class=\"text-gray-400\">No briefing text.</p>" : "<p>" + description.Replace("\n", "</p><p>") + "</p>")}</div>

                {(string.IsNullOrWhiteSpace(blueTask) ? "" : $"<h2 class=\"text-2xl font-semibold mt-12 mb-4 border-b border-blue-700 pb-2\">Blue Task</h2><div class=\"prose prose-invert\">{blueTask.Replace("\n", "<br>")}</div>")}
                {(string.IsNullOrWhiteSpace(redTask) ? "" : $"<h2 class=\"text-2xl font-semibold mt-12 mb-4 border-b border-red-700 pb-2\">Red Task</h2><div class=\"prose prose-invert\">{redTask.Replace("\n", "<br>")}</div>")}

                <h2 class=""text-2xl font-semibold mt-16 mb-6"">📸 Briefing Images</h2>
                <div class=""image-grid"">{string.Join("", Directory.GetFiles(imagesDir).Select(f => $@"<div class=""bg-gray-900 rounded-xl overflow-hidden border border-gray-700""><img src=""images/{Path.GetFileName(f)}"" class=""w-full""><div class=""px-4 py-2 text-xs text-gray-400 font-mono"">{Path.GetFileName(f)}</div></div>"))}</div>

                {kneeboardHtml}

                {GeneratePlayerSlotsHtmlSection(mission)}

                {GenerateFlightsWithWaypointsHtmlSection(mission)}

                {GenerateRequiredModsHtmlSection(mission)}

                {GenerateAtoHtmlSection(mission)}

                {GenerateUnitsAndTargetsHtmlSection(mission)}

                {GenerateWeatherHtmlSection(mission, options.Units)}

                {GenerateOrderOfBattleHtmlSection(mission)}

                <div class=""mt-16 text-center text-xs text-gray-500"">
                Generated by DCS Mission Reader • {DateTime.Now:yyyy-MM-dd HH:mm}
                </div>
                </div></body></html>";

            File.WriteAllText(htmlPath, html);
        }

        /// <summary>
        /// Generates a JSON summary of the mission data, including the mission name, map, date, start time, version, briefing text, blue and red tasks, and lists of briefing images and kneeboards. The summary is saved as "mission_summary.json" in the specified report directory. This JSON file provides a structured overview of the mission that can be easily consumed by other applications or for further analysis. The method ensures that all relevant information is included in the summary while keeping it concise and focused on the key details of the mission.
        /// </summary>
        /// <param name="reportDir"></param>
        /// <param name="sortie"></param>
        /// <param name="mapName"></param>
        /// <param name="fullDate"></param>
        /// <param name="startTime"></param>
        /// <param name="version"></param>
        /// <param name="description"></param>
        /// <param name="blueTask"></param>
        /// <param name="redTask"></param>
        /// <param name="imagesDir"></param>
        /// <param name="kneeboardsDir"></param>
        private static void GenerateJsonSummary(string reportDir, string sortie, string mapName, string fullDate,
            string startTime, string version, string description, string blueTask, string redTask,
            string imagesDir, string kneeboardsDir)
        {
            var summary = new
            {
                name = sortie,
                map = mapName,
                date = fullDate,
                startTime,
                version,
                briefing = description,
                blueTask,
                redTask,
                briefingImages = Directory.GetFiles(imagesDir).Select(Path.GetFileName).ToList(),
                kneeboards = Directory.GetFiles(kneeboardsDir, "*.*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(kneeboardsDir, f)).ToList()
            };

            string jsonPath = Path.Combine(reportDir, "mission_summary.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"   📄 mission_summary.json created");
        }

        /// <summary>
        /// Generates a full export of the mission data, including the mission table, dictionary table, and raw files.
        /// The export is saved as "mission_full.json" in the specified report directory.
        /// </summary>
        /// <param name="reportDir">The directory where the export will be saved.</param>
        /// <param name="mission">The mission table.</param>
        /// <param name="dictTable">The dictionary table.</param>
        /// <param name="tempDir">The temporary directory containing raw files.</param>
        private static void GenerateFullExport(string reportDir, Table mission, Table dictTable, string tempDir)
        {
            var fullData = new
            {
                // ✅ Correct way to wrap a MoonSharp Table into a DynValue
                mission = TableToObject(DynValue.NewTable(mission)),
                dictionary = dictTable != null ? TableToObject(DynValue.NewTable(dictTable)) : null,
                rawFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(tempDir, f)).ToList()
            };

            string fullPath = Path.Combine(reportDir, "mission_full.json");
            File.WriteAllText(fullPath, JsonSerializer.Serialize(fullData, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"   📄 mission_full.json created (complete raw mission data)");
        }

        /// <summary>
        /// Returns a C# object representation of a MoonSharp DynValue, converting tables to dictionaries and preserving primitive types. This method is used to convert the mission and dictionary tables into a format that can be easily serialized to JSON for the full export. It recursively processes tables, converting them into nested dictionaries, while other data types are returned as their native C# types (e.g., numbers, strings, booleans). This allows for a complete and accurate representation of the mission data in the JSON output.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private static object? TableToObject(DynValue val)
        {
            return val.Type switch
            {
                DataType.Table => TableToDictionary(val.Table),
                DataType.Number => val.Number,
                DataType.String => val.String,
                DataType.Boolean => val.Boolean,
                DataType.Nil => null,
                _ => val.ToString()
            };
        }

        /// <summary>
        /// Converts a MoonSharp Table into a C# dictionary, recursively processing nested tables.
        /// </summary>
        /// <param name="table">The MoonSharp Table to convert.</param>
        /// <returns>A dictionary representing the table's key-value pairs.</returns>
        private static Dictionary<string, object?> TableToDictionary(Table table)
        {
            var result = new Dictionary<string, object?>();

            foreach (var pair in table.Pairs)
            {
                string key = DynValueKeyToString(pair.Key);
                result[key] = TableToObject(pair.Value);
            }

            return result;
        }

        /// <summary>
        /// Converts a MoonSharp DynValue key into a string representation, handling different data types.
        /// </summary>
        /// <param name="key">The DynValue key to convert.</param>
        /// <returns>A string representation of the key.</returns>
        private static string DynValueKeyToString(DynValue key)
        {
            return key.Type switch
            {
                DataType.String => key.String,
                DataType.Number => IsWholeNumber(key.Number)
                    ? ((long)key.Number).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : key.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DataType.Boolean => key.Boolean ? "true" : "false",
                DataType.Nil => "null",
                _ => key.ToString() ?? "null"
            };
        }

        /// <summary>
        /// Determines if a double value is a whole number.
        /// </summary>
        /// <param name="value">The double value to check.</param>
        /// <returns>True if the value is a whole number; otherwise, false.</returns>
        private static bool IsWholeNumber(double value)
        {
            return Math.Abs(value % 1) < double.Epsilon;
        }

        /// <summary>
        /// Sanitizes a file name by replacing invalid characters with underscores and trimming whitespace and trailing dots.
        /// </summary>
        /// <param name="name">The file name to sanitize.</param>
        /// <returns>The sanitized file name.</returns>
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "_");
            return name.Trim().TrimEnd('.');
        }

        /// <summary>
        /// Generates ATO or Air Tasking Order section for the HTML report. This section lists the groups of aircraft assigned to various tasks for each coalition (Blue, Red, Neutral). It extracts the relevant information from the mission table, including group names, tasks, aircraft types, quantities, and start times, and formats it into an HTML table. The section is styled using Tailwind CSS to match the overall design of the report. This provides a clear and organized overview of the air operations planned in the mission briefing.
        /// </summary>
        /// <param name="mission">The mission table containing the ATO data.</param>
        /// <returns>The HTML string representing the ATO section.</returns>
        private static string GenerateAtoHtmlSection(Table mission)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">✈️ Air Tasking Order (ATO)</h2>");

            var coalitionTable = mission.Get("coalition");
            if (coalitionTable.Type != DataType.Table)
            {
                sb.AppendLine(@"<p class=""text-yellow-400"">No coalition data found.</p>");
                return sb.ToString();
            }

            var coalition = coalitionTable.Table;

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };
            string[] colors = { "border-blue-700", "border-red-700", "border-gray-700" };

            for (int i = 0; i < sides.Length; i++)
            {
                var sideVal = coalition.Get(sides[i]);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{sides[i]}-400"">{sideNames[i]} Coalition</h3>");
                sb.AppendLine(@"<table class=""w-full border-collapse text-sm"">");
                sb.AppendLine(@"<thead><tr class=""bg-gray-800""><th class=""p-3 text-left"">Group</th><th class=""p-3 text-left"">Task</th><th class=""p-3 text-left"">Aircraft</th><th class=""p-3 text-center"">Qty</th><th class=""p-3 text-center"">Start Time</th></tr></thead><tbody>");

                int groupCount = 0;

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

                    // Check plane, helicopter, and ship groups
                    foreach (var category in new[] { "plane", "helicopter", "ship" })
                    {
                        var catVal = country.Get(category);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");   // note: singular "group" in current DCS
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var groupPair in groupListVal.Table.Pairs)
                        {
                            if (groupPair.Value.Type != DataType.Table) continue;
                            var group = groupPair.Value.Table;

                            string groupName = group.Get("name")?.String ?? "Unknown";
                            string task = group.Get("task")?.String ?? "None";
                            string aircraftType = "Unknown";
                            int unitsCount = 0;

                            var unitsVal = group.Get("units");
                            if (unitsVal.Type == DataType.Table && unitsVal.Table.Length > 0)
                            {
                                unitsCount = unitsVal.Table.Length;
                                var firstUnit = unitsVal.Table.Get(1)?.Table;
                                if (firstUnit != null)
                                    aircraftType = firstUnit.Get("type")?.String ?? "Unknown";
                            }

                            double startTime = group.Get("start_time")?.Number ?? 0;
                            string startStr = startTime > 0
                                ? $"{(int)(startTime / 3600):D2}:{(int)((startTime % 3600) / 60):D2}"
                                : "—";

                            sb.AppendLine($@"<tr class=""border-t border-gray-700"">
                        <td class=""p-3"">{groupName}</td>
                        <td class=""p-3"">{task}</td>
                        <td class=""p-3 font-mono"">{aircraftType}</td>
                        <td class=""p-3 text-center"">{unitsCount}</td>
                        <td class=""p-3 text-center"">{startStr}</td>
                    </tr>");

                            groupCount++;
                        }
                    }
                }

                sb.AppendLine("</tbody></table>");
                Console.WriteLine($"   → {sideNames[i]} ATO: {groupCount} groups found");  // debug output
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates the "Required Mods" section for the HTML report based on the "requiredModules" field in the mission table. This section lists any additional mods that are required to load the mission in DCS. The method checks if the "requiredModules" field is present and is a table, then extracts the mod names and formats them into a visually distinct section using Tailwind CSS. If no required mods are found, it displays a message indicating that no additional mods are needed. This section helps players understand if they need to install any specific mods to play the mission as intended.
        /// </summary>
        /// <param name="mission">The mission table containing the "requiredModules" field.</param>
        /// <returns>A string containing the HTML representation of the required mods section.</returns>
        private static string GenerateRequiredModsHtmlSection(Table mission)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<h2 id=\"required-mods\">Required Mods</h2>");

            var reqVal = mission.Get("requiredModules");
            if (reqVal.Type == DataType.Table)
            {
                var reqTable = reqVal.Table;
                var mods = new List<string>();

                // requiredModules is a string-keyed table (not numeric)
                foreach (var pair in reqTable.Pairs)
                {
                    string modName = pair.Key?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(modName))
                        mods.Add(modName);
                }

                if (mods.Count > 0)
                {
                    sb.AppendLine("<div class=\"bg-amber-100 border border-amber-300 rounded-xl p-5\">");
                    sb.AppendLine("<p class=\"font-semibold text-amber-800 mb-3\">This mission requires the following mods to load in DCS:</p>");
                    sb.AppendLine("<ul class=\"list-disc pl-5 space-y-1 text-amber-700\">");

                    foreach (var mod in mods.OrderBy(m => m))
                    {
                        sb.AppendLine($"<li><strong>{mod}</strong></li>");
                    }

                    sb.AppendLine("</ul>");
                    sb.AppendLine("</div>");
                }
                else
                {
                    sb.AppendLine("<p class=\"text-green-600\"><em>No additional mods are required for this mission.</em></p>");
                }
            }
            else
            {
                sb.AppendLine("<p class=\"text-green-600\"><em>No additional mods are required for this mission.</em></p>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a weather section for the HTML report based on the weather data available in the mission table. This section includes information about cloud conditions, wind at different altitudes, visibility, QNH (pressure), and temperature. The method extracts the relevant weather parameters from the mission data and formats them into a visually appealing layout using Tailwind CSS. If no weather data is found, it displays a warning message. This section provides valuable insights into the environmental conditions that players can expect during the mission.
        /// </summary>
        /// <param name="mission">The mission table containing weather data.</param>
        /// <returns>A string containing the HTML representation of the weather section.</returns>
        // UPDATED METHOD: GenerateWeatherHtmlSection
        private static string GenerateWeatherHtmlSection(Table mission, UnitsSystem units)
        {
            var weatherVal = mission.Get("weather");
            if (weatherVal.Type != DataType.Table)
                return "<p class=\"text-yellow-400\">No weather data found.</p>";

            var w = weatherVal.Table;

            string clouds = w.Get("clouds")?.Table?.Get("preset")?.String ?? "Clear";
            double cloudBaseM = w.Get("clouds")?.Table?.Get("base")?.Number ?? 0;
            double cloudThicknessM = w.Get("clouds")?.Table?.Get("thickness")?.Number ?? 0;

            var wind = w.Get("wind")?.Table;
            string windSurface = GetWindString(wind?.Get("atGround"), units);
            string wind2000 = GetWindString(wind?.Get("at2000"), units);
            string wind8000 = GetWindString(wind?.Get("at8000"), units);

            double visibilityM = w.Get("visibility")?.Table?.Get("distance")?.Number ?? 80000;
            string visStr = GetVisibilityString(visibilityM, units);

            double qnh = w.Get("qnh")?.Number ?? 760;
            double tempC = w.Get("season")?.Table?.Get("temperature")?.Number ?? 15;

            string cloudLine = "";
            if (cloudBaseM > 0)
            {
                double baseVal = units == UnitsSystem.Metric ? cloudBaseM / 1000 : cloudBaseM * 3.28084;
                double thickVal = units == UnitsSystem.Metric ? cloudThicknessM / 1000 : cloudThicknessM * 3.28084;
                string unit = units == UnitsSystem.Metric ? "km" : "ft";
                cloudLine = $"Base {baseVal:F1} {unit} • Thickness {thickVal:F1} {unit}";
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">🌤️ Weather</h2>");
            sb.AppendLine(@"<div class=""grid grid-cols-2 md:grid-cols-3 gap-6 text-sm"">");

            sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-5"">
        <div class=""text-gray-400 text-xs mb-1"">CLOUDS</div>
        <div class=""text-2xl font-semibold"">{clouds}</div>");
            if (!string.IsNullOrEmpty(cloudLine))
                sb.AppendLine($@"<div class=""text-xs text-gray-400"">{cloudLine}</div>");
            sb.AppendLine("</div>");

            sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-5"">
        <div class=""text-gray-400 text-xs mb-1"">WIND</div>
        <div>Surface: <span class=""font-semibold"">{windSurface}</span></div>
        <div>2000 ft: <span class=""font-semibold"">{wind2000}</span></div>
        <div>8000 ft: <span class=""font-semibold"">{wind8000}</span></div>
    </div>");

            string pressureUnit = units == UnitsSystem.Metric ? "hPa" : "inHg";
            double pressureVal = units == UnitsSystem.Metric ? qnh : qnh * 0.02953;
            double tempVal = units == UnitsSystem.Metric ? tempC : (tempC * 9.0 / 5) + 32;
            string tempUnit = units == UnitsSystem.Metric ? "°C" : "°F";

            sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-5"">
        <div class=""text-gray-400 text-xs mb-1"">VISIBILITY / QNH</div>
        <div>Visibility: <span class=""font-semibold"">{visStr}</span></div>
        <div>QNH: <span class=""font-semibold"">{pressureVal:F2} {pressureUnit}</span></div>
        <div>Temperature: <span class=""font-semibold"">{tempVal:F0} {tempUnit}</span></div>
    </div>");

            sb.AppendLine("</div>");
            return sb.ToString();
        }

        /// <summary>
        /// Generates a string representation of the wind conditions based on the provided wind data and units system.
        /// </summary>
        /// <param name="windDyn">The dynamic value containing wind data.</param>
        /// <param name="units">The units system to use for the output.</param>
        /// <returns>A string representing the wind direction and speed.</returns>
        private static string GetWindString(DynValue windDyn, UnitsSystem units)
        {
            if (windDyn?.Type != DataType.Table) return "—";
            var t = windDyn.Table;
            double speedMs = t.Get("speed")?.Number ?? 0;
            double dir = t.Get("dir")?.Number ?? 0;

            if (units == UnitsSystem.Metric)
                return $"{(int)dir}° / {speedMs:F1} m/s";
            else
                return $"{(int)dir}° / {(speedMs * 1.94384):F0} kt";
        }

        /// <summary>
        /// Generates a string representation of the visibility based on the provided distance and units system.
        /// </summary>
        /// <param name="meters">The visibility distance in meters.</param>
        /// <param name="units">The units system to use for the output.</param>
        /// <returns>A string representing the visibility.</returns>    
        private static string GetVisibilityString(double meters, UnitsSystem units)
        {
            if (meters >= 80000) return "Unlimited";

            if (units == UnitsSystem.Metric)
                return $"{meters / 1000:F0} km";
            else
                return $"{(meters * 0.000621371):F0} mi";
        }

        /// <summary>
        /// Generates an Order of Battle (OOB) section for the HTML report based on the coalition data available in the mission table. This section provides a summary of the forces present in the mission, categorized by coalition (Blue, Red, Neutral). It counts the number of aircraft, helicopters, ships, ground units, and static objects for each side and presents this information in a visually appealing format using Tailwind CSS. If no coalition data is found, it displays a warning message. This section gives players a quick overview of the military assets involved in the mission.
        /// Ground Units Breakdown table has been removed (now redundant with Units & Targets section).
        /// </summary>
        /// <param name="mission">The mission table containing coalition data.</param>
        /// <returns>A string containing the HTML representation of the Order of Battle section.</returns>
        private static string GenerateOrderOfBattleHtmlSection(Table mission)
        {
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table)
                return "<p class=\"text-yellow-400\">No coalition data found for OOB.</p>";

            var coalition = coalitionVal.Table;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">📊 Order of Battle (OOB)</h2>");

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };

            foreach (var side in sides)
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                var aircraftCounts = new Dictionary<string, int>();
                int totalAircraft = 0, totalHelos = 0, totalShips = 0, totalGround = 0, totalStatics = 0;

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

                    // Aircraft & Helicopters
                    foreach (var cat in new[] { "plane", "helicopter" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groupList = catVal.Table.Get("group");
                        if (groupList.Type != DataType.Table) continue;

                        foreach (var gPair in groupList.Table.Pairs)
                        {
                            if (gPair.Value.Type != DataType.Table) continue;
                            var group = gPair.Value.Table;

                            var unitsVal = group.Get("units");
                            if (unitsVal.Type != DataType.Table) continue;

                            foreach (var uPair in unitsVal.Table.Pairs)
                            {
                                if (uPair.Value.Type != DataType.Table) continue;
                                var unit = uPair.Value.Table;
                                string type = unit.Get("type")?.String ?? "Unknown";

                                if (cat == "plane")
                                {
                                    aircraftCounts[type] = aircraftCounts.GetValueOrDefault(type) + 1;
                                    totalAircraft++;
                                }
                                else
                                {
                                    aircraftCounts[type] = aircraftCounts.GetValueOrDefault(type) + 1;
                                    totalHelos++;
                                }
                            }
                        }
                    }

                    // Ships
                    var shipVal = country.Get("ship");
                    if (shipVal.Type == DataType.Table)
                    {
                        var shipGroups = shipVal.Table.Get("group");
                        if (shipGroups.Type == DataType.Table)
                            totalShips += shipGroups.Table.Length;
                    }

                    // Ground units (vehicle)
                    var vehicleVal = country.Get("vehicle");
                    if (vehicleVal.Type == DataType.Table)
                    {
                        var vehicleGroups = vehicleVal.Table.Get("group");
                        if (vehicleGroups.Type == DataType.Table)
                            totalGround += vehicleGroups.Table.Length;   // we count groups, not individual units here
                    }

                    // Statics
                    var staticVal = country.Get("static");
                    if (staticVal.Type == DataType.Table)
                    {
                        var staticGroups = staticVal.Table.Get("group");
                        if (staticGroups.Type == DataType.Table)
                            totalStatics += staticGroups.Table.Length;
                    }
                }

                // Build HTML for this side
                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{side}-400"">{sideNames[sides.ToList().IndexOf(side)]} Coalition</h3>");
                sb.AppendLine(@"<div class=""grid grid-cols-5 gap-4 mb-6 text-center"">");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">AIRCRAFT</div><div class=""text-3xl font-bold"">{totalAircraft + totalHelos}</div></div>");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">SHIPS</div><div class=""text-3xl font-bold"">{totalShips}</div></div>");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">GROUND</div><div class=""text-3xl font-bold"">{totalGround}</div></div>");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">STATICS</div><div class=""text-3xl font-bold"">{totalStatics}</div></div>");
                sb.AppendLine("</div>");

                // Detailed Aircraft Breakdown (kept)
                if (aircraftCounts.Count > 0)
                {
                    sb.AppendLine(@"<details class=""mb-8""><summary class=""cursor-pointer text-lg font-medium mb-2"">Aircraft Breakdown</summary><table class=""w-full border-collapse text-sm""><thead><tr class=""bg-gray-800""><th class=""p-3 text-left"">Type</th><th class=""p-3 text-center"">Count</th></tr></thead><tbody>");
                    foreach (var kvp in aircraftCounts.OrderByDescending(k => k.Value))
                        sb.AppendLine($@"<tr class=""border-t border-gray-700""><td class=""p-3"">{kvp.Key}</td><td class=""p-3 text-center font-semibold"">{kvp.Value}</td></tr>");
                    sb.AppendLine("</tbody></table></details>");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a section for units and targets based on the coalition data in the mission table. This section lists the groups of sea and ground units for each coalition (Blue, Red, Neutral), along with the number of units in each group. The method extracts the relevant information from the mission data and formats it into an HTML layout using Tailwind CSS. If no units are found, it displays a message indicating that no ground or sea units are present in the mission. This section provides players with an overview of the forces they will encounter or control during the mission.
        /// </summary>
        /// <param name="mission">The mission data table.</param>
        /// <returns>An HTML string representing the units and targets section.</returns>
        /// <summary>
        private static string GenerateUnitsAndTargetsHtmlSection(Table mission)
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">📦 Units & Targets</h2>");

            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table)
            {
                sb.AppendLine(@"<p class=""text-emerald-400 italic"">No ground, sea, or static units found in this mission.</p>");
                return sb.ToString();
            }

            var coalition = coalitionVal.Table;
            string[] sides = { "blue", "red", "neutral" };
            string[] sideEmojis = { "🔵", "🔴", "⚪" };
            string[] sideNames = { "BLUE COALITION", "RED COALITION", "NEUTRAL COALITION" };

            bool anyUnitsFound = false;

            for (int i = 0; i < sides.Length; i++)
            {
                var side = sides[i];
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                var sideHtml = new StringBuilder();
                bool hasUnitsForThisSide = false;

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

                    foreach (var category in new[] { "ship", "vehicle", "static" })
                    {
                        var catVal = country.Get(category);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var gPair in groupListVal.Table.Pairs)
                        {
                            if (gPair.Value.Type != DataType.Table) continue;
                            var g = gPair.Value.Table;

                            string groupName = g.Get("name")?.String ?? "Unknown Group";
                            double gx = g.Get("x")?.Number ?? 0;
                            double gy = g.Get("y")?.Number ?? 0;

                            var unitCounts = new Dictionary<string, int>();
                            var unitsVal = g.Get("units");
                            if (unitsVal.Type == DataType.Table)
                            {
                                for (int u = 1; u <= unitsVal.Table.Length; u++)
                                {
                                    var unit = unitsVal.Table.Get(u);
                                    if (unit.Type != DataType.Table) continue;
                                    string uType = unit.Table.Get("type")?.String ?? "Unknown";
                                    unitCounts[uType] = unitCounts.GetValueOrDefault(uType, 0) + 1;
                                }
                            }

                            int totalUnits = unitCounts.Values.Sum();
                            var unitList = unitCounts.OrderByDescending(kv => kv.Value)
                                                     .Select(kv => $"{kv.Key} ×{kv.Value}")
                                                     .ToList();

                            string unitInfo = unitList.Count > 0 ? string.Join(", ", unitList) : "No units listed";

                            string icon = category == "ship" ? "⚓" : category == "vehicle" ? "🪖" : "📦";
                            string colorClass = side == "blue" ? "blue" : side == "red" ? "red" : "slate";

                            sideHtml.AppendLine($@"
                        <div class=""flex items-start gap-6 p-6 bg-slate-800 border border-slate-700 rounded-3xl hover:border-{colorClass}-500 transition-all"">
                            <div class=""text-5xl flex-shrink-0 mt-1"">{icon}</div>
                            <div class=""flex-1"">
                                <div class=""font-semibold text-slate-100 text-lg"">{groupName}</div>
                                <div class=""text-xs font-mono text-slate-400 mt-1"">{gx:F0}, {gy:F0}</div>
                                <div class=""text-sm text-slate-300 mt-3"">{unitInfo}</div>
                            </div>
                            <div class=""text-right text-xs font-medium text-slate-400"">
                                {totalUnits}<br/><span class=""text-[10px]"">UNITS</span>
                            </div>
                        </div>");

                            hasUnitsForThisSide = true;
                            anyUnitsFound = true;
                        }
                    }
                }

                if (hasUnitsForThisSide)
                {
                    // Clean single-line h3 (no more split AppendLine or escaping issues)
                    sb.AppendLine($@"<h3 class=""text-{(side == "blue" ? "blue" : side == "red" ? "red" : "slate")}-400 text-lg font-semibold mt-10 mb-5 flex items-center gap-2"">
                          <span class=""text-2xl"">{sideEmojis[i]}</span>
                          {sideNames[i]}
                        </h3>");
                    sb.Append(sideHtml);
                }
            }

            if (!anyUnitsFound)
                sb.AppendLine(@"<p class=""text-emerald-400 italic"">No ground, sea, or static units found in this mission.</p>");

            return sb.ToString();
        }

        /// <summary>
        /// Generates a section for player and client spawn spots based on the coalition data in the mission table. This section lists the groups that contain player or client units, along with their assigned tasks and aircraft types. It provides a clear overview of where players can expect to spawn in the mission, categorized by coalition (Blue, Red, Neutral). The method extracts the relevant information from the mission data and formats it into an HTML layout using Tailwind CSS. If no player slot data is found, it displays a warning message. This section is particularly useful for players to quickly identify their starting positions and roles in the mission.
        /// </summary>
        /// <param name="mission">The mission table containing coalition data.</param>
        /// <returns>An HTML string representing the player and client spawn spots section.</returns>
        private static string GeneratePlayerSlotsHtmlSection(Table mission)
        {
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table)
                return "<p class=\"text-yellow-400\">No player slot data found.</p>";

            var coalition = coalitionVal.Table;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">🧑‍✈️ Player &amp; Client Spawn Spots</h2>");

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };

            foreach (var side in sides)
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{side}-400"">{sideNames[sides.ToList().IndexOf(side)]} Coalition</h3>");

                bool hasSlots = false;

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

                    foreach (var cat in new[] { "plane", "helicopter" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var groupPair in groupListVal.Table.Pairs)
                        {
                            if (groupPair.Value.Type != DataType.Table) continue;
                            var group = groupPair.Value.Table;

                            string groupName = group.Get("name")?.String ?? "Unknown";
                            string task = group.Get("task")?.String ?? "None";

                            var unitsVal = group.Get("units");
                            if (unitsVal.Type != DataType.Table) continue;

                            int clientCount = 0;
                            string aircraftType = "Unknown";

                            foreach (var unitPair in unitsVal.Table.Pairs)
                            {
                                if (unitPair.Value.Type != DataType.Table) continue;
                                var unit = unitPair.Value.Table;

                                string skill = unit.Get("skill")?.String ?? "";
                                bool isClient = skill == "Client" || unit.Get("playerCanDrive")?.Boolean == true;

                                if (isClient)
                                {
                                    clientCount++;
                                    if (aircraftType == "Unknown")
                                        aircraftType = unit.Get("type")?.String ?? "Unknown";
                                }
                            }

                            if (clientCount > 0)
                            {
                                hasSlots = true;
                                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4 mb-4"">
                            <div class=""flex justify-between"">
                                <div><span class=""font-semibold"">{groupName}</span> — {aircraftType}</div>
                                <div class=""text-sm text-gray-400"">{task} • {clientCount} client slot{(clientCount > 1 ? "s" : "")}</div>
                            </div>
                        </div>");
                            }
                        }
                    }
                }

                if (!hasSlots)
                    sb.AppendLine(@"<p class=""text-gray-400 italic"">No player/client slots found for this coalition.</p>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a section for flights and waypoints based on the coalition data in the mission table. This section lists the groups of aircraft along with their assigned tasks and detailed waypoint information, including actions, altitudes, speeds, and coordinates. It provides a comprehensive overview of the planned flight routes and maneuvers for each coalition (Blue, Red, Neutral). The method extracts the relevant information from the mission data and formats it into an HTML layout using Tailwind CSS. If no flight waypoint data is found, it displays a warning message. This section is particularly useful for players to understand the intended flight paths and objectives for the various groups in the mission.
        /// </summary>
        /// <param name="mission">The mission table containing coalition data.</param>
        /// <returns>An HTML string representing the flights and waypoints section.</returns>
        private string GenerateFlightsWithWaypointsHtmlSection(Table mission)
        {
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table)
                return "<p class=\"text-yellow-400\">No flight waypoint data found.</p>";

            string theatre = mission.Get("theatre")?.String ?? "Caucasus";

            var coalition = coalitionVal.Table;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">🛫 Flights &amp; Waypoints</h2>");

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };

            foreach (var side in sides)
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{side}-400"">{sideNames[sides.ToList().IndexOf(side)]} Coalition</h3>");

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

                    foreach (var cat in new[] { "plane", "helicopter" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var groupPair in groupListVal.Table.Pairs)
                        {
                            if (groupPair.Value.Type != DataType.Table) continue;
                            var group = groupPair.Value.Table;

                            string groupName = group.Get("name")?.String ?? "Unknown";
                            string task = group.Get("task")?.String ?? "None";
                            int unitCount = group.Get("units")?.Table?.Length ?? 0;
                            string aircraft = "Unknown";

                            var unitsVal = group.Get("units");
                            if (unitsVal.Type == DataType.Table && unitsVal.Table.Length > 0)
                                aircraft = unitsVal.Table.Get(1)?.Table?.Get("type")?.String ?? "Unknown";

                            sb.AppendLine($@"<details class=""mb-8 bg-gray-900 rounded-2xl p-5""><summary class=""cursor-pointer font-medium text-lg flex items-center gap-3"">✈️ {groupName} — {aircraft} ({unitCount}×) • {task}</summary>");

                            var routeVal = group.Get("route");
                            if (routeVal.Type == DataType.Table)
                            {
                                var pointsVal = routeVal.Table.Get("points");
                                if (pointsVal.Type == DataType.Table && pointsVal.Table.Length > 0)
                                {
                                    sb.AppendLine(@"<div class=""mt-4""><table class=""w-full border-collapse text-xs""><thead><tr class=""bg-gray-800""><th class=""p-2 text-center"">#</th><th class=""p-2"">Action</th><th class=""p-2 text-right"">Alt (ft)</th><th class=""p-2 text-right"">Speed (kt)</th><th class=""p-2"">DCS (x, y)</th><th class=""p-2"">LAT / LONG</th></tr></thead><tbody>");

                                    var waypoints = new List<(double x, double y, double alt, double speed, string action)>();
                                    int idx = 1;

                                    foreach (var pPair in pointsVal.Table.Pairs)
                                    {
                                        if (pPair.Value.Type != DataType.Table) continue;
                                        var point = pPair.Value.Table;

                                        double x = point.Get("x")?.Number ?? 0;
                                        double y = point.Get("y")?.Number ?? 0;
                                        double alt = point.Get("alt")?.Number ?? 0;
                                        double speed = point.Get("speed")?.Number ?? 0;
                                        string action = point.Get("action")?.String ?? "Turning Point";

                                        var (lat, lon) = GetLatLonFromDcs(x, y, theatre);

                                        sb.AppendLine($@"<tr class=""border-t border-gray-700"">
                                    <td class=""p-2 text-center font-semibold"">{idx}</td>
                                    <td class=""p-2"">{action}</td>
                                    <td class=""p-2 text-right"">{(alt * 3.28084):F0}</td>
                                    <td class=""p-2 text-right"">{(speed * 1.94384):F0}</td>
                                    <td class=""p-2 font-mono"">{x:F0}, {y:F0}</td>
                                    <td class=""p-2 font-mono"">{lat:F6}, {lon:F6}</td>
                                </tr>");

                                        waypoints.Add((x, y, alt, speed, action));
                                        idx++;
                                    }
                                    sb.AppendLine("</tbody></table></div>");

                                    if (waypoints.Count > 1)
                                        sb.AppendLine(GenerateFlightSvgMap(waypoints, groupName));
                                }
                            }
                            sb.AppendLine("</details>");
                        }
                    }
                }
            }

            return sb.ToString();
        }

        private static string GenerateFlightSvgMap(List<(double x, double y, double alt, double speed, string action)> waypoints, string groupName)
        {
            if (waypoints.Count == 0) return "";

            // Find bounding box
            double minX = waypoints.Min(p => p.x);
            double maxX = waypoints.Max(p => p.x);
            double minY = waypoints.Min(p => p.y);
            double maxY = waypoints.Max(p => p.y);

            double width = maxX - minX;
            double height = maxY - minY;
            if (width < 100) width = 100;
            if (height < 100) height = 100;

            // Scale to SVG viewBox (800x500)
            const int svgWidth = 800;
            const int svgHeight = 500;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($@"<details class=""mt-6""><summary class=""cursor-pointer text-sm font-medium mb-2"">🗺️ Interactive Route Map for {groupName}</summary>");
            sb.AppendLine($@"<svg width=""{svgWidth}"" height=""{svgHeight}"" viewBox=""0 0 {svgWidth} {svgHeight}"" class=""border border-gray-700 rounded-xl bg-gray-950"">");

            // Draw route line
            sb.Append(@"<polyline points=""");
            foreach (var wp in waypoints)
            {
                double px = (wp.x - minX) / width * svgWidth;
                double py = svgHeight - (wp.y - minY) / height * svgHeight; // Y is inverted in SVG
                sb.Append($"{px:F0},{py:F0} ");
            }
            sb.AppendLine(@""" fill=""none"" stroke=""#22d3ee"" stroke-width=""4"" stroke-linejoin=""round"" stroke-linecap=""round"" />");

            // Draw points + labels
            int idx = 1;
            foreach (var wp in waypoints)
            {
                double px = (wp.x - minX) / width * svgWidth;
                double py = svgHeight - (wp.y - minY) / height * svgHeight;

                sb.AppendLine($@"<circle cx=""{px:F0}"" cy=""{py:F0}"" r=""8"" fill=""#22d3ee"" />");
                sb.AppendLine($@"<text x=""{px + 12:F0}"" y=""{py + 5:F0}"" fill=""#67e8f9"" font-size=""11"" font-family=""monospace"">{idx}</text>");
                idx++;
            }

            sb.AppendLine("</svg></details>");
            return sb.ToString();
        }

        private (double lat, double lon) GetLatLonFromDcs(double x, double y, string theatre)
        {
            return converter.Convert(x, y, theatre);
        }

        /// <summary>
        /// Generates a KML file for Google Earth that visualizes the flight routes of aircraft groups in the DCS mission. 
        /// The method extracts the route information from the mission table, including the waypoints for each group of planes and helicopters, 
        /// and converts the DCS x/y coordinates to latitude and longitude based on the theatre. 
        /// It then constructs a KML file with placemarks for each group and line strings representing their routes. 
        /// The generated KML file is saved in the specified report directory with a name based on the sortie. 
        /// This allows players to easily view and analyze the flight paths in Google Earth, providing a spatial understanding of the mission's air operations.
        ///
        /// UPDATED: Now also includes all ground targets (vehicles, ships, statics) as colored placemarks using the same coalition colors.
        /// UPDATED: Reads ALL waypoints from every aircraft group.
        /// UPDATED: Uses exact 90° CCW conversion calibrated to your points.
        /// </summary>
        /// <param name="reportDir">The directory where the KML file will be saved.</param>
        /// <param name="sortie">The name of the sortie.</param>
        /// <param name="mission">The mission table containing the mission data.</param>
        /// <param name="theatre">The theatre of operations.</param>
        private void GenerateKmlExport(string reportDir, string sortie, Table mission, string theatre)
        {
            string kmlPath = Path.Combine(reportDir, SanitizeFileName(sortie) + ".kml");
            var kml = new System.Text.StringBuilder();
            kml.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            kml.AppendLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2"">");
            kml.AppendLine("<Document>");
            kml.AppendLine($"<name>{EscapeForKml(sortie + " - DCS Mission Routes & Targets")}</name>");

            // Coalition-specific color palettes (ABGR format) - expanded for many routes
            var blueColors = new[] {
                "ff00ffff", "ff00b3ff", "ff0080ff", "ff0066ff", "ff0040ff",
                "ff00ccff", "ff00aaff", "ff0099ff", "ff0077ff", "ff0055ff",
                "ff3399ff", "ff66aaff", "ff99ccff"
            };
            var redColors = new[] {
                "ff0000ff", "ff0066ff", "ff00a5ff", "ffff00ff", "ffff3399",
                "ffff0066", "ffff3366", "ffff6633", "ffff9933", "ffffcc33",
                "ffff6600", "ffff9900", "ffffcc00"
            };
            var neutralColors = new[] {
                "ff00ff80", "ff80ff00", "ffccff00", "ffffff00", "ffccff66",
                "ff99ff66", "ff66ff99", "ff33ffcc", "ff00ffcc", "ff66ffcc",
                "ff99ffcc", "ffccffcc", "ffffffcc"
            };

            int blueIdx = 0, redIdx = 0, neutralIdx = 0;

            // Waypoint placemark style
            kml.AppendLine(@"<Style id=""wpStyle""><IconStyle><scale>1.2</scale><Icon><href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            // Ground target style
            kml.AppendLine(@"<Style id=""groundStyle""><IconStyle><scale>1.0</scale><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            // Coalition-colored ground unit markers
            kml.AppendLine(@"<Style id=""blueGroundStyle""><IconStyle><scale>1.0</scale><color>ff00ffff</color><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            kml.AppendLine(@"<Style id=""redGroundStyle""><IconStyle><scale>1.0</scale><color>ff0000ff</color><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            kml.AppendLine(@"<Style id=""neutralGroundStyle""><IconStyle><scale>1.0</scale><color>ffffffff</color><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            // Threat circle styles
            kml.AppendLine(@"<Style id=""yellowCircleStyle""><LineStyle><color>ff00ffff</color><width>3</width></LineStyle><PolyStyle><color>2000ffff</color></PolyStyle></Style>");
            kml.AppendLine(@"<Style id=""redCircleStyle""><LineStyle><color>ff0000ff</color><width>3</width></LineStyle><PolyStyle><color>200000ff</color></PolyStyle></Style>");

            static string EscapeForKml(string text)
            {
                if (string.IsNullOrEmpty(text)) return string.Empty;
                return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
            }

            // === PROCESS ALL COALITIONS ===
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type == DataType.Table)
            {
                var coalition = coalitionVal.Table;
                string[] sides = { "blue", "red", "neutrals" };
                foreach (var side in sides)
                {
                    var sideVal = coalition.Get(side);
                    if (sideVal.Type != DataType.Table) continue;

                    var countryListVal = sideVal.Table.Get("country");
                    if (countryListVal.Type != DataType.Table) continue;

                    foreach (var countryPair in countryListVal.Table.Pairs)
                    {
                        if (countryPair.Value.Type != DataType.Table) continue;
                        var country = countryPair.Value.Table;

                        // === AIR ROUTES & ALL WAYPOINTS ===
                        foreach (var cat in new[] { "plane", "helicopter" })
                        {
                            var catVal = country.Get(cat);
                            if (catVal.Type != DataType.Table) continue;
                            var groupListVal = catVal.Table.Get("group");
                            if (groupListVal.Type != DataType.Table) continue;

                            foreach (var groupPair in groupListVal.Table.Pairs)
                            {
                                if (groupPair.Value.Type != DataType.Table) continue;
                                var group = groupPair.Value.Table;
                                string groupName = group.Get("name")?.String ?? "Unknown Group";

                                string color = side == "blue" ? blueColors[blueIdx++ % blueColors.Length] :
                                               side == "red" ? redColors[redIdx++ % redColors.Length] :
                                               neutralColors[neutralIdx++ % neutralColors.Length];

                                var routeVal = group.Get("route");
                                if (routeVal.Type != DataType.Table) continue;
                                var pointsVal = routeVal.Table.Get("points");
                                if (pointsVal.Type != DataType.Table || pointsVal.Table.Length == 0) continue;

                                // Full route LineString
                                kml.AppendLine("<Placemark>");
                                kml.AppendLine($"<name>{EscapeForKml(groupName)} Route</name>");
                                kml.AppendLine($@"<Style><LineStyle><color>{color}</color><width>5</width></LineStyle></Style>");
                                kml.AppendLine("<LineString>");
                                kml.AppendLine("<tessellate>1</tessellate>");
                                kml.AppendLine("<coordinates>");
                                for (int i = 1; i <= pointsVal.Table.Length; i++)
                                {
                                    var pt = pointsVal.Table.Get(i);
                                    if (pt.Type != DataType.Table) continue;
                                    double x = pt.Table.Get("x")?.Number ?? 0;
                                    double y = pt.Table.Get("y")?.Number ?? 0;
                                    double alt = pt.Table.Get("alt")?.Number ?? 0;

                                    // Apply Origin Calibration
                                    var (lat, lon) = GetLatLonFromDcs(x, y, theatre);
                                    kml.AppendLine($" {lon:F8},{lat:F8},{alt:F0}");
                                }
                                kml.AppendLine("</coordinates>");
                                kml.AppendLine("</LineString>");
                                kml.AppendLine("</Placemark>");

                                // Individual waypoint placemarks (ALL of them)
                                for (int i = 1; i <= pointsVal.Table.Length; i++)
                                {
                                    var pt = pointsVal.Table.Get(i);
                                    if (pt.Type != DataType.Table) continue;
                                    double x = pt.Table.Get("x")?.Number ?? 0;
                                    double y = pt.Table.Get("y")?.Number ?? 0;
                                    double alt = pt.Table.Get("alt")?.Number ?? 0;
                                    string wpName = pt.Table.Get("name")?.String ?? $"WP{i}";

                                    // Apply Origin Calibration
                                    var (lat, lon) = GetLatLonFromDcs(x, y, theatre);
                                    kml.AppendLine("<Placemark>");
                                    kml.AppendLine($"<name>{EscapeForKml(wpName)}</name>");
                                    kml.AppendLine($"<description><![CDATA[Waypoint {i}<br/>Alt: {alt:F0} m]]></description>");
                                    kml.AppendLine("<styleUrl>#wpStyle</styleUrl>");
                                    kml.AppendLine("<Point>");
                                    kml.AppendLine($"<coordinates>{lon:F8},{lat:F8},{alt:F0}</coordinates>");
                                    kml.AppendLine("</Point>");
                                    kml.AppendLine("</Placemark>");
                                }
                            }
                        }

                        // === GROUND TARGETS (vehicles, ships, statics) ===
                        foreach (var cat in new[] { "vehicle", "ship", "static" })
                        {
                            var catVal = country.Get(cat);
                            if (catVal.Type != DataType.Table) continue;
                            var groupListVal = catVal.Table.Get("group");
                            if (groupListVal.Type != DataType.Table) continue;

                            foreach (var gPair in groupListVal.Table.Pairs)
                            {
                                if (gPair.Value.Type != DataType.Table) continue;
                                var g = gPair.Value.Table;
                                string groupName = g.Get("name")?.String ?? "Unknown Target";
                                double gx = g.Get("x")?.Number ?? 0;
                                double gy = g.Get("y")?.Number ?? 0;

                                var unitCounts = new Dictionary<string, int>();
                                var unitsVal = g.Get("units");
                                if (unitsVal.Type == DataType.Table)
                                {
                                    for (int u = 1; u <= unitsVal.Table.Length; u++)
                                    {
                                        var unit = unitsVal.Table.Get(u);
                                        if (unit.Type != DataType.Table) continue;
                                        string uType = unit.Table.Get("type")?.String ?? "Unknown";
                                        unitCounts[uType] = unitCounts.GetValueOrDefault(uType, 0) + 1;
                                    }
                                }
                                string unitInfo = unitCounts.Count > 0
                                    ? string.Join(", ", unitCounts.Select(kv => $"{kv.Key} ×{kv.Value}"))
                                    : cat.ToUpper();

                                var (lat, lon) = GetLatLonFromDcs(gx, gy, theatre);

                                string markerStyle = side == "blue" ? "#blueGroundStyle" : side == "red" ? "#redGroundStyle" : "#neutralGroundStyle";

                                kml.AppendLine("<Placemark>");
                                kml.AppendLine($"<name>{EscapeForKml(groupName)}</name>");
                                kml.AppendLine($"<description><![CDATA[{cat.ToUpper()} • {unitInfo}<br/>{gx:F0}, {gy:F0}]]></description>");
                                kml.AppendLine($"<styleUrl>{markerStyle}</styleUrl>");
                                kml.AppendLine("<Point>");
                                kml.AppendLine($"<coordinates>{lon:F8},{lat:F8},0</coordinates>");
                                kml.AppendLine("</Point>");
                                kml.AppendLine("</Placemark>");

                                // Threat circles
                                bool hasThreat = false;
                                double maxTrack = 0, maxFire = 0;
                                foreach (var type in unitCounts.Keys)
                                {
                                    string key = type.ToLower().Replace(" ", "").Replace("-", "");
                                    if (threatService.GetThreatRanges(key) is (double track, double fire))
                                    {
                                        hasThreat = true;
                                        maxTrack = Math.Max(maxTrack, track);
                                        maxFire = Math.Max(maxFire, fire);
                                    }
                                }
                                if (hasThreat && maxTrack > 0)
                                {
                                    // Convert meters to NM (1 NM = 1852 meters)
                                    double maxTrackNm = maxTrack / 1852.0;
                                    double maxFireNm = maxFire / 1852.0;

                                    string yellowCoords = GenerateCircle(lat, lon, maxTrackNm);
                                    kml.AppendLine("<Placemark>");
                                    kml.AppendLine($"<name>{EscapeForKml(groupName)} - Tracking Range</name>");
                                    kml.AppendLine("<styleUrl>#yellowCircleStyle</styleUrl>");
                                    kml.AppendLine("<Polygon><outerBoundaryIs><LinearRing><coordinates>" + yellowCoords + "</coordinates></LinearRing></outerBoundaryIs></Polygon>");
                                    kml.AppendLine("</Placemark>");

                                    if (maxFire > 0 && maxFire < maxTrack)
                                    {
                                        string redCoords = GenerateCircle(lat, lon, maxFireNm);
                                        kml.AppendLine("<Placemark>");
                                        kml.AppendLine($"<name>{EscapeForKml(groupName)} - Firing Range</name>");
                                        kml.AppendLine("<styleUrl>#redCircleStyle</styleUrl>");
                                        kml.AppendLine("<Polygon><outerBoundaryIs><LinearRing><coordinates>" + redCoords + "</coordinates></LinearRing></outerBoundaryIs></Polygon>");
                                        kml.AppendLine("</Placemark>");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            kml.AppendLine("</Document>");
            kml.AppendLine("</kml>");

            File.WriteAllText(kmlPath, kml.ToString());
            Console.WriteLine($" 🗺️ KML created with FULL routes + ALL waypoints + ground targets → {kmlPath}");
        }

        private static string GenerateCircle(double centerLat, double centerLon, double radiusNm, int segments = 36)
        {
            var sb = new System.Text.StringBuilder();
            double radiusRad = radiusNm * 1852.0 / 6371000.0;
            for (int i = 0; i <= segments; i++)
            {
                double angle = i * 2 * Math.PI / segments;
                double dLat = radiusRad * Math.Cos(angle);
                double dLon = radiusRad * Math.Sin(angle) / Math.Cos(centerLat * Math.PI / 180.0);
                double lat = centerLat + dLat * (180.0 / Math.PI);
                double lon = centerLon + dLon * (180.0 / Math.PI);
                sb.Append($"{lon:F8},{lat:F8},0 ");
            }
            return sb.ToString().Trim();
        }

        #endregion Private helper methods
    }
}
