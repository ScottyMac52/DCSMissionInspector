using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements the IMissionProcessor interface to process DCS mission files based on the provided options. This class is responsible for reading the mission files, extracting relevant data, and generating output in various formats (HTML, JSON) as specified by the command line options. It handles the core logic of processing each mission file, including error handling and cleanup of temporary files.
    /// </summary>
    public class MissionProcessor : IMissionProcessor
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
                ProcessSingleMission(mizPath, options, anyExport);
                Console.WriteLine(new string('-', 80));
            }

            Console.WriteLine("✅ All done.");
        }

        #endregion IMissionProcessor implementation

        #region Private helper methods

        /// <summary>
        /// Processes a single mission file (.miz). It extracts the mission data, resolves localized strings, and generates output based on the specified options. The method handles the entire lifecycle of processing a mission file, including error handling and cleanup of temporary files.
        /// </summary>
        /// <param name="mizPath">The path to the mission file (.miz) to process.</param>
        /// <param name="options">The application options specifying what outputs to generate.</param>
        /// <param name="anyExport">Indicates whether any export option is enabled.</param>
        private void ProcessSingleMission(string mizPath, AppOptions options, bool anyExport)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DCSMission_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                Console.WriteLine($"📦 Processing: {Path.GetFileName(mizPath)}");
                ZipFile.ExtractToDirectory(mizPath, tempDir, overwriteFiles: true);

                string missionFile = Path.Combine(tempDir, "mission");
                if (!File.Exists(missionFile))
                {
                    Console.WriteLine("⚠️  No 'mission' file found.");
                    return;
                }

                var script = new Script();
                script.DoFile(missionFile);
                Table mission = script.Globals.Get("mission").Table;

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
                        GenerateKmlExport(reportDir, sortie, mission);

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
        private static void GenerateHtmlReport(string reportDir, string imagesDir, string kneeboardsDir, string sortie,
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

{GenerateAtoHtmlSection(mission)}

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
        /// Converts a MoonSharp Table (wrapped in a DynValue) into a standard C# object (Dictionary, List, or primitive) that can be easily serialized to JSON. This method recursively processes the table and its nested structures, converting them into native C# types. It handles tables, numbers, strings, booleans, and nil values appropriately. This is essential for generating the full export of the mission data in a format that can be easily consumed and analyzed outside of the Lua environment.
        /// </summary>
        /// <param name="val">The DynValue to convert.</param>
        /// <returns>The converted C# object.</returns>
        private static object? TableToObject(DynValue val)
        {
            return val.Type switch
            {
                DataType.Table => val.Table.Pairs.ToDictionary(
                    p => p.Key.ToString() ?? "null",
                    p => TableToObject(p.Value)),
                DataType.Number => val.Number,
                DataType.String => val.String,
                DataType.Boolean => val.Boolean,
                DataType.Nil => null,
                _ => val.ToString()
            };
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
                var groundCounts = new Dictionary<string, int>();
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
                        {
                            foreach (var gPair in vehicleGroups.Table.Pairs)
                            {
                                if (gPair.Value.Type != DataType.Table) continue;
                                var group = gPair.Value.Table;
                                var unitsVal = group.Get("units");
                                if (unitsVal.Type == DataType.Table)
                                {
                                    foreach (var uPair in unitsVal.Table.Pairs)
                                    {
                                        if (uPair.Value.Type != DataType.Table) continue;
                                        var unit = uPair.Value.Table;
                                        string type = unit.Get("type")?.String ?? "Unknown";
                                        groundCounts[type] = groundCounts.GetValueOrDefault(type) + 1;
                                        totalGround++;
                                    }
                                }
                            }
                        }
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

                // Detailed Aircraft
                if (aircraftCounts.Count > 0)
                {
                    sb.AppendLine(@"<details class=""mb-8""><summary class=""cursor-pointer text-lg font-medium mb-2"">Aircraft Breakdown</summary><table class=""w-full border-collapse text-sm""><thead><tr class=""bg-gray-800""><th class=""p-3 text-left"">Type</th><th class=""p-3 text-center"">Count</th></tr></thead><tbody>");
                    foreach (var kvp in aircraftCounts.OrderByDescending(k => k.Value))
                        sb.AppendLine($@"<tr class=""border-t border-gray-700""><td class=""p-3"">{kvp.Key}</td><td class=""p-3 text-center font-semibold"">{kvp.Value}</td></tr>");
                    sb.AppendLine("</tbody></table></details>");
                }

                // Detailed Ground
                if (groundCounts.Count > 0)
                {
                    sb.AppendLine(@"<details class=""mb-8""><summary class=""cursor-pointer text-lg font-medium mb-2"">Ground Units Breakdown</summary><table class=""w-full border-collapse text-sm""><thead><tr class=""bg-gray-800""><th class=""p-3 text-left"">Type</th><th class=""p-3 text-center"">Count</th></tr></thead><tbody>");
                    foreach (var kvp in groundCounts.OrderByDescending(k => k.Value))
                        sb.AppendLine($@"<tr class=""border-t border-gray-700""><td class=""p-3"">{kvp.Key}</td><td class=""p-3 text-center font-semibold"">{kvp.Value}</td></tr>");
                    sb.AppendLine("</tbody></table></details>");
                }
            }

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
        private static string GenerateFlightsWithWaypointsHtmlSection(Table mission)
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

        /// <summary>
        /// Gets the latitude and longitude coordinates from DCS x/y coordinates based on the specified theatre. DCS uses a local coordinate system for each theatre, where (0,0) is typically at the center of the map. This method applies a conversion based on reference points and scaling factors for each theatre to approximate the corresponding latitude and longitude. The conversion is not exact but provides a reasonable estimate for displaying coordinates in a more familiar format. The method supports major DCS theatres such as Caucasus, Persian Gulf, Syria, Mariana, Normandy, Channel, Sinai, and Iraq. If an unrecognized theatre is provided, it defaults to using the Caucasus conversion as a fallback.
        /// </summary>
        /// <param name="x">The x-coordinate in DCS local coordinate system.</param>
        /// <param name="y">The y-coordinate in DCS local coordinate system.</param>
        /// <param name="theatre">The name of the DCS theatre.</param>
        /// <returns>A tuple containing the latitude and longitude coordinates.</returns>
        // CLEAN LAT/LONG CONVERSION (replace ALL old conversion methods with this)
        private static (double lat, double lon) GetLatLonFromDcs(double x, double y, string theatre)
        {
            string t = theatre.ToLowerInvariant().Trim();

            return t switch
            {
                "caucasus" => ConvertGeneric(x, y, 42.0, 42.0),
                "persiangulf" => ConvertGeneric(x, y, 25.0, 55.0),
                "syria" => ConvertGeneric(x, y, 34.0, 38.0),
                "marianas" => ConvertGeneric(x, y, 15.0, 145.0),
                "ww2marianas" => ConvertGeneric(x, y, 15.0, 145.0),
                "normandy" => ConvertGeneric(x, y, 49.0, 0.0),
                "thechannel" => ConvertGeneric(x, y, 51.0, 0.0),
                "sinai" => ConvertGeneric(x, y, 29.0, 33.0),
                "iraq" => ConvertGeneric(x, y, 33.0, 44.0),
                "kola" => ConvertGeneric(x, y, 68.0, 30.0),
                "afghanistan" => ConvertGeneric(x, y, 34.0, 69.0),
                "germany" => ConvertGeneric(x, y, 50.0, 10.0),
                "coldwargermany" => ConvertGeneric(x, y, 50.0, 10.0),

                // Falklands / South Atlantic
                "southatlantic" => ConvertSouthAtlantic(x, y),
                "south atlantic" => ConvertSouthAtlantic(x, y),
                "falklands" => ConvertSouthAtlantic(x, y),

                _ => ConvertGeneric(x, y, 42.0, 42.0)
            };
        }

        /*

        // FINAL FIXED VERSION - Rotation + Mirror calibrated to your waypoints
        private static (double lat, double lon) ConvertSouthAtlantic(double x, double y)
        {
            // Falklands: 90° clockwise rotation + mirror (negate X after rotation)
            double newX = y;
            double newY = x;        // ← changed from -x to +x to fix the mirror

            var result = ConvertGeneric(newX, newY, -51.6857, -57.7776);

            Console.WriteLine($"   [FALKLANDS FIXED] DCS ({x:F0}, {y:F0}) → LAT/LON ({result.lat:F6}, {result.lon:F6})");
            return result;
        }

        // MIRROR-FIXED VERSION for Falklands
        private static (double lat, double lon) ConvertSouthAtlantic(double x, double y)
        {
            // Falklands: 90° clockwise rotation + mirror flip
            double newX = y;
            double newY = x;          // ← this is the mirror correction (was -x before)

            var result = ConvertGeneric(newX, newY, -51.6857, -57.7776);

            Console.WriteLine($"   [FALKLANDS MIRROR FIX] DCS ({x:F0}, {y:F0}) → LAT/LON ({result.lat:F6}, {result.lon:F6})");
            return result;
        }

        */

        // VERTICAL FLIP VERSION - 90° clockwise rotation + vertical flip across horizontal center
        private static (double lat, double lon) ConvertSouthAtlantic(double x, double y)
        {
            // 90° clockwise rotation
            double newX = y;
            double newY = x;          // ← this is the vertical flip (was -x before)

            // Precise reference point calculated from your two waypoints
            //var result = ConvertGeneric(newX, newY, -50.884445, -59.113519);

            // Calibrated reference point (your Port Stanley runway)
            //var result = ConvertGeneric(newX, newY, -51.6857, -57.7776);

            // Tuned reference point to shift the entire route south-west onto the runway
            //var result = ConvertGeneric(newX, newY, -51.6857, -58.05);

            // Reference point shifted south-west to place takeoff exactly on Runway 27
            // var result = ConvertGeneric(newX, newY, -52.4857, -58.865);

            // Tuned reference to move takeoff point onto Runway 27
            // var result = ConvertGeneric(newX, newY, -51.6857, -58.92);

            // Exact reference point calculated from your WP0 DCS (89025, 93868) → real runway coordinates
            // var result = ConvertGeneric(newX, newY, -52.48559, -59.15666);

            // Final micro-tune to place takeoff exactly on Runway 27
            // var result = ConvertGeneric(newX, newY, -51.71, -58.96);

            // Exact reference point from your WP0 (89025, 93868) → real runway
            //var result = ConvertGeneric(newX, newY, -52.48559, -59.15666);

            // Strong south-west shift to land exactly on Runway 27
            //var result = ConvertGeneric(newX, newY, -51.69, -58.85);

            // Shifted 1447 meters south to place takeoff on Runway 27, with good overall route alignment
            // var result = ConvertGeneric(newX, newY, -52.4986, -59.15666);

            // Shift North 1447 meters to place takeoff on Runway 27
            // var result = ConvertGeneric(newX, newY, -52.48559, -59.15666);

            // Shifted 600 meters west from the previous reference
            // var result = ConvertGeneric(newX, newY, -52.48559, -59.16551);

            // 50 meters east (right) shift from previous reference
            var result = ConvertGeneric(newX, newY, -52.48559, -59.16478);

            Console.WriteLine($"   [FALKLANDS VERTICAL FLIP] DCS ({x:F0}, {y:F0}) → LAT/LON ({result.lat:F6}, {result.lon:F6})");
            return result;
        }

        private static (double lat, double lon) ConvertGeneric(double x, double y, double refLat, double refLon)
        {
            const double metersPerDegLat = 111320.0;
            double metersPerDegLon = 111320.0 * Math.Cos(refLat * Math.PI / 180.0);

            double lat = refLat + (y / metersPerDegLat);
            double lon = refLon + (x / metersPerDegLon);

            return (lat, lon);
        }

        /// <summary>
        /// Generates a KML file for Google Earth that visualizes the flight routes of aircraft groups in the DCS mission. The method extracts the route information from the mission table, including the waypoints for each group of planes and helicopters, and converts the DCS x/y coordinates to latitude and longitude based on the theatre. It then constructs a KML file with placemarks for each group and line strings representing their routes. The generated KML file is saved in the specified report directory with a name based on the sortie. This allows players to easily view and analyze the flight paths in Google Earth, providing a spatial understanding of the mission's air operations. 
        /// </summary>
        /// <param name="reportDir">The directory where the KML file will be saved.</param>
        /// <param name="sortie">The name of the sortie, used to name the KML file.</param>
        /// <param name="mission">The mission data containing the flight routes.</param>
        // UPDATED: GenerateKmlExport - waypoint placemarks with names + numbers
        private static void GenerateKmlExport(string reportDir, string sortie, Table mission)
        {
            string theatre = mission.Get("theatre")?.String ?? "caucasus";
            string kmlPath = Path.Combine(reportDir, SanitizeFileName(sortie) + ".kml");

            var kml = new System.Text.StringBuilder();
            kml.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            kml.AppendLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2"">");
            kml.AppendLine("<Document>");
            kml.AppendLine($"<name>{sortie} - DCS Mission Routes</name>");

            // Route line style
            kml.AppendLine(@"<Style id=""routeStyle""><LineStyle><color>ff22d3ee</color><width>5</width></LineStyle></Style>");
            // Waypoint placemark style
            kml.AppendLine(@"<Style id=""wpStyle""><IconStyle><scale>1.2</scale><Icon><href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");

            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type == DataType.Table)
            {
                var coalition = coalitionVal.Table;
                string[] sides = { "blue", "red", "neutral" };

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

                                var routeVal = group.Get("route");
                                if (routeVal.Type != DataType.Table) continue;
                                var pointsVal = routeVal.Table.Get("points");
                                if (pointsVal.Type != DataType.Table || pointsVal.Table.Length == 0) continue;

                                // === ROUTE LINE ===
                                kml.AppendLine("<Placemark>");
                                kml.AppendLine($"<name>{groupName} Route</name>");
                                kml.AppendLine("<styleUrl>#routeStyle</styleUrl>");
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

                                    var (lat, lon) = GetLatLonFromDcs(x, y, theatre);
                                    kml.AppendLine($"      {lon:F8},{lat:F8},{alt:F0}");
                                }

                                kml.AppendLine("</coordinates>");
                                kml.AppendLine("</LineString>");
                                kml.AppendLine("</Placemark>");

                                // === WAYPOINT PLACEMARKS (with name or number) ===
                                for (int i = 1; i <= pointsVal.Table.Length; i++)
                                {
                                    var pt = pointsVal.Table.Get(i);
                                    if (pt.Type != DataType.Table) continue;

                                    double x = pt.Table.Get("x")?.Number ?? 0;
                                    double y = pt.Table.Get("y")?.Number ?? 0;
                                    double alt = pt.Table.Get("alt")?.Number ?? 0;

                                    string wpName = pt.Table.Get("name")?.String;
                                    if (string.IsNullOrWhiteSpace(wpName))
                                        wpName = $"WP{i}";

                                    string description = $"Waypoint {i}<br/>Alt: {alt:F0} m";

                                    var (lat, lon) = GetLatLonFromDcs(x, y, theatre);

                                    kml.AppendLine("<Placemark>");
                                    kml.AppendLine($"<name>{wpName}</name>");
                                    kml.AppendLine($"<description>{description}</description>");
                                    kml.AppendLine("<styleUrl>#wpStyle</styleUrl>");
                                    kml.AppendLine("<Point>");
                                    kml.AppendLine($"<coordinates>{lon:F8},{lat:F8},{alt:F0}</coordinates>");
                                    kml.AppendLine("</Point>");
                                    kml.AppendLine("</Placemark>");
                                }
                            }
                        }
                    }
                }
            }

            kml.AppendLine("</Document>");
            kml.AppendLine("</kml>");

            File.WriteAllText(kmlPath, kml.ToString());
            Console.WriteLine($"   🗺️  KML created with waypoint placemarks → {kmlPath}");
        }

        #endregion Private helper methods
    }
}
