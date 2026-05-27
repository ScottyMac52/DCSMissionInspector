using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using System.Text;

namespace DcsMissionReader.Services.Generators
{
    /// <summary>
    /// Implements the IMissionExportStrategy interface to generate an HTML report for a DCS mission. This class is responsible for gathering the necessary data from the mission, such as kneeboards and images, and then building an HTML string that represents the report. Finally, it writes the generated HTML to a file in the specified report directory. By implementing this strategy, the application can provide users with a visually appealing and easily accessible report of their DCS missions in HTML format.
    /// </summary>
    public class HtmlReportGenerator(IFileManagementService fileManagementService) : IMissionExportStrategy
    {
        public bool ShouldExport(AppOptions options) => options.CreateHtml;

        public void Export(MissionContext context)
        {
            string imagesDir = Path.Combine(context.ReportDir, "images");
            string kneeboardsDir = Path.Combine(context.ReportDir, "kneeboards");

            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(kneeboardsDir);

            // Re-use your existing copy logic (ensure these helpers are moved here too)
            fileManagementService.CopyImages(context.TempDir, imagesDir);
            int kneeboardCount = fileManagementService.CopyKneeboards(context.TempDir, kneeboardsDir);

            // Fetch metadata
            string mapName = File.Exists(Path.Combine(context.TempDir, "theatre")) ? File.ReadAllText(Path.Combine(context.TempDir, "theatre")).Trim() : "Unknown";
            string description = MissionUtils.Resolve(context.MissionTable.Get("descriptionText"), null);
            string blueTask = MissionUtils.Resolve(context.MissionTable.Get("descriptionBlueTask"), null);
            string redTask = MissionUtils.Resolve(context.MissionTable.Get("descriptionRedTask"), null);
            var dateTable = context.MissionTable.Get("date").Table;
            string fullDate = $"{(int)dateTable.Get("Year").Number}-{(int)dateTable.Get("Month").Number:D2}-{(int)dateTable.Get("Day").Number:D2}";
            double startSec = context.MissionTable.Get("start_time").Number;
            string startTime = $"{(int)(startSec / 3600):D2}:{(int)((startSec % 3600) / 60):D2}";
            string version = context.MissionTable.Get("version").ToString();    
            string html = BuildHtml(imagesDir, kneeboardsDir, context.Sortie, mapName, fullDate, startTime, version, description, blueTask, redTask, kneeboardCount, context.MissionTable, context.Options);
            File.WriteAllText(Path.Combine(context.ReportDir, "index.html"), html);
        }

        private string BuildHtml(string imagesDir, string kneeboardsDir, string sortie, string mapName, string fullDate, string startTime, string version, string description, string blueTask, string redTask, int kneeboardCount, Table mission, AppOptions options)
        {
            // The CSS logic that provides your layout containers, cards, and borders
            string css = @"
    body { font-family: system-ui, sans-serif; }
    .image-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(400px, 1fr)); gap: 1rem; }
    /* Force exact column sizing */
    table { border-collapse: collapse; table-layout: fixed; width: 100%; }
    th, td { padding: 0.75rem; overflow: hidden; }
    th:nth-child(1), td:nth-child(1) { width: 5%; }  /* # */
    th:nth-child(2), td:nth-child(2) { width: 35%; } /* Action */
    th:nth-child(3), td:nth-child(3) { width: 15%; text-align: right; } /* Alt */
    th:nth-child(4), td:nth-child(4) { width: 15%; text-align: right; } /* Speed */
    th:nth-child(5), td:nth-child(5) { width: 30%; } /* DCS (x, y) */
    .cursor-pointer { cursor: pointer; }";

            string kneeboardHtml = "";
            if (kneeboardCount > 0)
            {
                var kbFiles = Directory.GetFiles(kneeboardsDir, "*.*", SearchOption.AllDirectories).OrderBy(f => f).ToList();
                kneeboardHtml = $@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">📋 Custom Kneeboards ({kneeboardCount} pages)</h2><div class=""image-grid"">";
                foreach (var f in kbFiles)
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    string relativePath = Path.GetRelativePath(kneeboardsDir, f);
                    string preview = ext == ".pdf" ? @"<div class=""h-64 flex items-center justify-center bg-gray-800 text-4xl"">📄</div>" : $@"<img src=""kneeboards/{relativePath}"" class=""w-full"">";
                    kneeboardHtml += $@"<div class=""bg-gray-900 rounded-xl overflow-hidden border border-gray-700"">{preview}<div class=""px-4 py-2 text-xs text-gray-400 font-mono"">{relativePath}</div></div>";
                }
                kneeboardHtml += "</div>";
            }

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <title>{sortie}</title>
    <script src=""https://cdn.tailwindcss.com""></script>
    <style>{css}</style>
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

        {(string.IsNullOrWhiteSpace(blueTask) ? "" : $@"<h2 class=""text-2xl font-semibold mt-12 mb-4 border-b border-blue-700 pb-2"">Blue Task</h2><div class=""prose prose-invert"">{blueTask.Replace("\n", "<br>")}</div>")}
        {(string.IsNullOrWhiteSpace(redTask) ? "" : $@"<h2 class=""text-2xl font-semibold mt-12 mb-4 border-b border-red-700 pb-2"">Red Task</h2><div class=""prose prose-invert"">{redTask.Replace("\n", "<br>")}</div>")}

        <h2 class=""text-2xl font-semibold mt-16 mb-6"">📸 Briefing Images</h2>
        <div class=""image-grid"">{string.Join("", Directory.GetFiles(imagesDir).Select(f => $@"<div class=""bg-gray-900 rounded-xl overflow-hidden border border-gray-700""><img src=""images/{Path.GetFileName(f)}"" class=""w-full""><div class=""px-4 py-2 text-xs text-gray-400 font-mono"">{Path.GetFileName(f)}</div></div>"))}</div>
        
        {kneeboardHtml}
        
        {GeneratePlayerSlotsHtmlSection(mission)}
        {GenerateFlightsWithWaypointsHtmlSection(mission)}
        {GenerateAtoHtmlSection(mission)}
        {GenerateUnitsAndTargetsHtmlSection(mission)}
        {GenerateWeatherHtmlSection(mission, options.Units)}
        {GenerateOrderOfBattleHtmlSection(mission)}

        <div class=""mt-16 text-center text-xs text-gray-500"">Generated by DCS Mission Reader • {DateTime.Now:yyyy-MM-dd HH:mm}</div>
    </div>
</body>
</html>";
        }
        /// <summary>
        /// Generates ATO or Air Tasking Order section for the HTML report. This section lists the groups of aircraft assigned to various tasks for each coalition (Blue, Red, Neutral). It extracts the relevant information from the mission table, including group names, tasks, aircraft types, quantities, and start times, and formats it into an HTML table. The section is styled using Tailwind CSS to match the overall design of the report. This provides a clear and organized overview of the air operations planned in the mission briefing.
        /// </summary>
        /// <param name="mission">The mission table containing the ATO data.</param>
        /// <returns>The HTML string representing the ATO section.</returns>
        private static string GenerateAtoHtmlSection(Table mission)
        {
            var sb = new StringBuilder();
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

        private string GenerateFlightsWithWaypointsHtmlSection(Table mission)
        {
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table)
                return "<p class=\"text-yellow-400\">No flight waypoint data found.</p>";

            var coalition = coalitionVal.Table;
            var sb = new StringBuilder();
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
                    var country = countryPair.Value.Table;
                    foreach (var cat in new[] { "plane", "helicopter" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;
                        var groupListVal = catVal.Table.Get("group");
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var groupPair in groupListVal.Table.Pairs)
                        {
                            var group = groupPair.Value.Table;
                            string groupName = group.Get("name")?.String ?? "Unknown";
                            string task = group.Get("task")?.String ?? "None";
                            int unitCount = group.Get("units")?.Table?.Length ?? 0;
                            string aircraft = group.Get("units")?.Table?.Get(1)?.Table?.Get("type")?.String ?? "Unknown";

                            sb.AppendLine($@"<details class=""mb-8 bg-gray-900 rounded-2xl p-5""><summary class=""cursor-pointer font-medium text-lg flex items-center gap-3"">✈️ {groupName} — {aircraft} ({unitCount}×) • {task}</summary>");

                            var routeVal = group.Get("route");
                            if (routeVal.Type == DataType.Table)
                            {
                                var pointsVal = routeVal.Table.Get("points");
                                if (pointsVal.Type == DataType.Table && pointsVal.Table.Length > 0)
                                {
                                    sb.AppendLine(@"<div class=""mt-4""><table class=""w-full border-collapse text-xs""><thead><tr class=""bg-gray-800""><th class=""p-2 text-center"" style=""width: 5%"">#</th><th class=""p-2 text-left"" style=""width: 35%"">Action</th><th class=""p-2 text-right"" style=""width: 15%"">Alt (ft)</th><th class=""p-2 text-right"" style=""width: 15%"">Speed (kt)</th><th class=""p-2 text-left"" style=""width: 30%"">DCS (x, y)</th></tr></thead><tbody>");

                                    var waypoints = new List<(double x, double y, double alt, double speed, string action, string name, List<(double x, double y, string targetName)> targets)>();
                                    int idx = 1;

                                    foreach (var pPair in pointsVal.Table.Pairs)
                                    {
                                        var point = pPair.Value.Table;
                                        double x = point.Get("x")?.Number ?? 0;
                                        double y = point.Get("y")?.Number ?? 0;
                                        double alt = point.Get("alt")?.Number ?? 0;
                                        double speed = point.Get("speed")?.Number ?? 0;
                                        string action = point.Get("action")?.String ?? "Turning Point";
                                        string wpName = point.Get("name")?.String ?? $"WP{idx}";

                                        var waypointTargets = new List<(double x, double y, string targetName)>();
                                        var taskParams = point.Get("task")?.Table?.Get("params")?.Table;
                                        if (taskParams != null)
                                        {
                                            var tasks = taskParams.Get("tasks")?.Table;
                                            if (tasks != null) waypointTargets.AddRange(ProcessTargets(tasks, mission, wpName));
                                        }

                                        waypoints.Add((x, y, alt, speed, action, wpName, waypointTargets));

                                        sb.AppendLine($@"<tr class=""border-t border-gray-700""><td class=""p-2 text-center font-semibold"">{idx}</td><td class=""p-2 text-left"">{action}</td><td class=""p-2 text-right"">{(int)(alt * 3.28084)}</td><td class=""p-2 text-right"">{(int)(speed * 1.94384)}</td><td class=""p-2 text-left font-mono"">{x:F0}, {y:F0}</td></tr>");
                                        idx++;
                                    }
                                    sb.AppendLine("</tbody></table></div>");
                                    if (waypoints.Count > 1) sb.AppendLine(GenerateFlightSvgMap(waypoints, groupName));
                                }
                            }
                            sb.AppendLine("</details>");
                        }
                    }
                }
            }
            return sb.ToString();
        }

        private List<(double x, double y, string name)> ProcessTargets(Table tasks, Table mission, string wpName)
        {
            var targets = new List<(double x, double y, string name)>();

            foreach (var t in tasks.Pairs)
            {
                var currentTask = t.Value.Table;
                var p = currentTask.Get("params")?.Table;
                if (p == null) continue;

                string tName = "Target";
                bool foundTarget = false;
                double targetX = p.Get("x")?.Number ?? 0;
                double targetY = p.Get("y")?.Number ?? 0;

                /*
                 * CASE 1: Structured Attack Group 
                 * Logic: Targets an explicit Group ID as defined in the mission editor.
                 */
                double gid = p.Get("groupId")?.Number ?? 0;
                if (gid > 0)
                {
                    tName = MissionUtils.ResolveNameFromGroupId(mission, gid);

                    // FIX: If task params have 0,0, fetch coordinates from the actual group definition
                    if (targetX == 0 && targetY == 0)
                    {
                        var groupTable = FindGroupById(mission, gid);
                        var units = groupTable?.Get("units")?.Table;
                        if (units != null && units.Length > 0)
                        {
                            targetX = units.Get(1)?.Table.Get("x")?.Number ?? 0;
                            targetY = units.Get(1)?.Table.Get("y")?.Number ?? 0;
                        }
                    }
                    foundTarget = true;
                }
                /*
                 * CASE 2: Coordinate-based / Pre-programmed Strike (Fallback)
                 */
                else if (targetX != 0 || targetY != 0)
                {
                    string unitType = MissionUtils.FindUnitTypeAtLocation(mission, targetX, targetY);
                    tName = !string.IsNullOrEmpty(unitType) ? $"Strike: {unitType}" : $"Strike: {wpName}";
                    foundTarget = true;
                }

                if (foundTarget && (targetX != 0 || targetY != 0))
                {
                    targets.Add((targetX, targetY, tName));
                }
            }
            return targets;
        }

        private void DebugTaskStructure(Table tasks)
        {
            System.Diagnostics.Debug.WriteLine($"--- DEBUGGING TASK STRUCTURE: {tasks.Pairs.Count()} tasks found ---");
            foreach (var pair in tasks.Pairs)
            {
                var task = pair.Value.Table;
                string id = task.Get("id")?.String ?? "Unknown";
                System.Diagnostics.Debug.WriteLine($"Task ID: {id}");

                var paramsTable = task.Get("params")?.Table;
                if (paramsTable != null)
                {
                    var subTasks = paramsTable.Get("tasks")?.Table;
                    System.Diagnostics.Debug.WriteLine($"  - Has nested sub-tasks: {subTasks != null}");
                    if (subTasks != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - Sub-task count: {subTasks.Pairs.Count()}");
                    }
                }
            }
        }
        private Table FindGroupById(Table mission, double groupId)
        {
            var coalition = mission.Get("coalition").Table;
            foreach (var side in new[] { "blue", "red", "neutral" })
            {
                if (coalition.Get(side).Type != DataType.Table) continue;
                var countries = coalition.Get(side).Table.Get("country").Table;
                foreach (var c in countries.Pairs)
                {
                    var country = c.Value.Table;
                    foreach (var cat in new[] { "plane", "helicopter", "vehicle", "ship" })
                    {
                        if (country.Get(cat).Type != DataType.Table) continue;
                        var groups = country.Get(cat).Table.Get("group").Table;
                        foreach (var g in groups.Pairs)
                        {
                            if (g.Value.Table.Get("groupId")?.Number == groupId) return g.Value.Table;
                        }
                    }
                }
            }
            return null;
        }
        private static string GenerateFlightSvgMap(List<(double x, double y, double alt, double speed, string action, string name, List<(double x, double y, string targetName)> targets)> waypoints, string groupName)
        {
            if (waypoints == null || waypoints.Count == 0) return "";

            double minX = waypoints.Min(p => p.x);
            double maxX = waypoints.Max(p => p.x);
            double minY = waypoints.Min(p => p.y);
            double maxY = waypoints.Max(p => p.y);

            double scale = Math.Max(maxY - minY, maxX - minX);
            if (scale < 100) scale = 100;
            double padding = scale * 0.15;
            double effectiveScale = scale + (padding * 2);

            double ProjectX(double y) => (y - minY + padding) / effectiveScale * 800;
            double ProjectY(double x) => 500 - ((x - minX + padding) / effectiveScale * 500);

            var sb = new StringBuilder();
            sb.AppendLine($@"<details class=""mt-6""><summary class=""cursor-pointer text-sm font-medium mb-2"">🗺️ Interactive Route Map for {groupName}</summary>");
            sb.AppendLine($@"<svg width=""800"" height=""500"" viewBox=""0 0 800 500"" class=""border border-gray-700 rounded-xl bg-gray-950"">");

            sb.Append(@"<polyline points=""");
            foreach (var wp in waypoints) sb.Append($"{ProjectX(wp.y):F0},{ProjectY(wp.x):F0} ");
            sb.AppendLine(@""" fill=""none"" stroke=""#22d3ee"" stroke-width=""4"" />");

            foreach (var wp in waypoints)
            {
                foreach (var target in wp.targets)
                {
                    double tx = ProjectX(target.y);
                    double ty = ProjectY(target.x);
                    sb.AppendLine($@"<g><line x1=""{tx - 6}"" y1=""{ty - 6}"" x2=""{tx + 6}"" y2=""{ty + 6}"" stroke=""red"" stroke-width=""2""/><line x1=""{tx + 6}"" y1=""{ty - 6}"" x2=""{tx - 6}"" y2=""{ty + 6}"" stroke=""red"" stroke-width=""2""/>
            <text x=""{tx + 8}"" y=""{ty - 8}"" fill=""red"" font-size=""10"" font-family=""monospace"" font-weight=""bold"">{target.targetName}</text></g>");
                }
            }

            foreach (var group in waypoints.Select((wp, i) => new { wp, idx = i + 1 }).GroupBy(p => $"{Math.Round(p.wp.x / 10)},{Math.Round(p.wp.y / 10)}"))
            {
                var first = group.First();
                double px = ProjectX(first.wp.y);
                double py = ProjectY(first.wp.x);
                string tooltip = string.Join("\n", group.Select(g => $"{g.wp.name}: {g.wp.action}"));
                string label = string.Join("/", group.Select(g => g.wp.name));
                sb.AppendLine($@"<g class=""cursor-pointer""><circle cx=""{px:F0}"" cy=""{py:F0}"" r=""8"" fill=""{(group.Count() > 1 ? "#f59e0b" : "#22d3ee")}""><title>{tooltip}</title></circle>
        <text x=""{px + 12}"" y=""{py + 5}"" fill=""{(group.Count() > 1 ? "#f59e0b" : "#67e8f9")}"" font-size=""11"" font-family=""monospace"" pointer-events=""none"">{label}</text></g>");
            }
            sb.AppendLine("</svg></details>");
            return sb.ToString();
        }
    }
}