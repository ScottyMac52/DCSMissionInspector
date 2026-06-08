using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DcsMissionReader.Services.Generators
{
    public class HtmlReportGenerator(IFileManagementService fileManagementService, IThreatDatabaseService threatDatabaseService, IWeaponDatabaseService weaponService) : IMissionExportStrategy
    {
        public bool ShouldExport(AppOptions options) => options.CreateHtml;

        public void Export(MissionContext context)
        {
            string imagesDir = Path.Combine(context.ReportDir, "images");
            string kneeboardsDir = Path.Combine(context.ReportDir, "kneeboards");

            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(kneeboardsDir);

            fileManagementService.CopyImages(context.TempDir, imagesDir);
            int kneeboardCount = fileManagementService.CopyKneeboards(context.TempDir, kneeboardsDir);

            string mapName = File.Exists(Path.Combine(context.TempDir, "theatre")) ? File.ReadAllText(Path.Combine(context.TempDir, "theatre")).Trim() : "Unknown";
            string description = MissionUtils.Resolve(context.MissionTable.Get("descriptionText"), context.DictTable);
            string blueTask = MissionUtils.Resolve(context.MissionTable.Get("descriptionBlueTask"), context.DictTable);
            string redTask = MissionUtils.Resolve(context.MissionTable.Get("descriptionRedTask"), context.DictTable);
            var dateTable = context.MissionTable.Get("date").Table;
            string fullDate = $"{(int)dateTable.Get("Year").Number}-{(int)dateTable.Get("Month").Number:D2}-{(int)dateTable.Get("Day").Number:D2}";
            double startSec = context.MissionTable.Get("start_time").Number;
            string startTime = $"{(int)(startSec / 3600):D2}:{(int)((startSec % 3600) / 60):D2}";
            string version = context.MissionTable.Get("version").ToString();

            string html = BuildHtml(imagesDir, kneeboardsDir, context.Sortie, mapName, fullDate, startTime, version, description, blueTask, redTask, kneeboardCount, context.MissionTable, context.Options, threatDatabaseService.GetThreatData());
            File.WriteAllText(Path.Combine(context.ReportDir, "index.html"), html);
        }

        private string BuildHtml(string imagesDir, string kneeboardsDir, string sortie, string mapName, string fullDate, string startTime, string version, string description, string blueTask, string redTask, int kneeboardCount, Table mission, AppOptions options, Dictionary<string, ThreatData> threatDict)
        {
            var missionIndexer = new MissionIndexer(mission);

            string css = @"
body { font-family: system-ui, sans-serif; }
.image-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(400px, 1fr)); gap: 1rem; }
table { border-collapse: collapse; table-layout: fixed; width: 100%; }
th, td { padding: 0.75rem; overflow: hidden; }
th:nth-child(1), td:nth-child(1) { width: 5%; }
th:nth-child(2), td:nth-child(2) { width: 25%; }
th:nth-child(3), td:nth-child(3) { width: 25%; }
th:nth-child(4), td:nth-child(4) { width: 12%; text-align: right; }
th:nth-child(5), td:nth-child(5) { width: 12%; text-align: right; }
th:nth-child(6), td:nth-child(6) { width: 21%; }
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
        {GenerateRequiredModsHtmlSection(mission)}
        {GeneratePlayerSlotsHtmlSection(mission)}
        {GenerateFlightsWithWaypointsHtmlSection(mission, missionIndexer, threatDict)}
        {GenerateAtoHtmlSection(mission, threatDict)} 
        {GenerateUnitsAndTargetsHtmlSection(mission, threatDict)}
        {GenerateWeatherHtmlSection(mission, options.Units)}
        {GenerateOrderOfBattleHtmlSection(mission)}

        <div class=""mt-16 text-center text-xs text-gray-500"">Generated by DCS Mission Reader • {DateTime.Now:yyyy-MM-dd HH:mm}</div>
    </div>
</body>
</html>";
        }

        // ==========================================
        // ATO EXTRACTION & GENERATION
        // ==========================================
        public static List<AtoGroupData> ExtractAtoData(Table mission)
        {
            var atoList = new List<AtoGroupData>();
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return atoList;

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

                    foreach (var category in new[] { "plane", "helicopter", "ship" })
                    {
                        var catVal = country.Get(category);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");
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
                                if (firstUnit != null) aircraftType = firstUnit.Get("type")?.String ?? "Unknown";
                            }

                            double startTime = group.Get("start_time")?.Number ?? 0;
                            atoList.Add(new AtoGroupData(side, groupName, task, aircraftType, unitsCount, startTime));
                        }
                    }
                }
            }
            return atoList;
        }

        private string GenerateAtoHtmlSection(Table mission, Dictionary<string, ThreatData> threatDict)
        {
            var atoData = ExtractAtoWithLoadouts(mission);
            var sb = new StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">✈️ Air Tasking Order (ATO)</h2>");

            if (atoData.Count == 0) return sb.AppendLine(@"<p class=""text-yellow-400"">No coalition data found.</p>").ToString();

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };

            for (int i = 0; i < sides.Length; i++)
            {
                var sideGroups = atoData.Where(a => a.Coalition == sides[i]).ToList();
                if (sideGroups.Count == 0) continue;

                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{sides[i]}-400"">{sideNames[i]} Coalition</h3>");

                foreach (var group in sideGroups)
                {
                    string startStr = group.StartTimeSec > 0 ? $"{(int)(group.StartTimeSec / 3600):D2}:{(int)((group.StartTimeSec % 3600) / 60):D2}" : "—";

                    sb.AppendLine($@"<details class=""mb-4 bg-gray-900 rounded-xl p-4 border border-gray-800"">");
                    sb.AppendLine($@"<summary class=""cursor-pointer font-medium flex justify-between items-center outline-none"">");
                    sb.AppendLine($@"<div class=""flex items-center gap-3""><span class=""text-xl"">🚀</span> {group.GroupName} — <span class=""font-mono text-{sides[i]}-300"">{group.AircraftType}</span> ({group.UnitsCount}×) • {group.Task}</div>");
                    sb.AppendLine($@"<div class=""text-sm text-gray-400"">Start: {startStr}</div>");
                    sb.AppendLine($@"</summary>");

                    sb.AppendLine(@"<div class=""mt-4 border-t border-gray-700 pt-4"">");

                    if (group.Units.Count == 0)
                    {
                        sb.AppendLine(@"<div class=""text-sm text-gray-500 italic"">No unit details available.</div>");
                    }

                    foreach (var unit in group.Units)
                    {
                        sb.AppendLine($@"<div class=""mb-6 last:mb-0 bg-gray-950 p-4 rounded-lg border border-gray-800"">");
                        sb.AppendLine($@"<div class=""text-sm font-semibold text-gray-200 mb-3 border-b border-gray-800 pb-2"">Unit: <span class=""text-white"">{unit.UnitName}</span></div>");

                        sb.AppendLine($@"<div class=""flex gap-4 mb-3 text-xs"">");
                        sb.AppendLine($@"<div class=""bg-gray-800 px-2 py-1 rounded text-gray-300"">🔥 Flares: {unit.Flare}</div>");
                        sb.AppendLine($@"<div class=""bg-gray-800 px-2 py-1 rounded text-gray-300"">✨ Chaff: {unit.Chaff}</div>");

                        string gunDisplay = unit.Gun == 0 ? "100" : unit.Gun.ToString("F0");
                        sb.AppendLine($@"<div class=""bg-gray-800 px-2 py-1 rounded text-gray-300"">🔫 Gun: {gunDisplay}%</div>");
                        sb.AppendLine($@"</div>");

                        if (unit.Pylons.Count > 0)
                        {
                            sb.AppendLine(@"<div class=""grid grid-cols-[4rem_1fr] gap-2 text-xs text-left border-b border-gray-800 pb-1 mb-2 text-gray-500 font-semibold"">");
                            sb.AppendLine(@"<div>Pylon</div><div>Weapon / Store</div>");
                            sb.AppendLine(@"</div>");

                            foreach (var pylon in unit.Pylons)
                            {
                                sb.AppendLine($@"<div class=""grid grid-cols-[4rem_1fr] gap-2 text-xs text-left border-b border-gray-800/50 py-1 last:border-0"">");
                                sb.AppendLine($@"<div class=""font-mono text-gray-400"">#{pylon.Pylon}</div>");
                                sb.AppendLine($@"<div class=""font-medium text-blue-200"">{pylon.Weapon}</div>");
                                sb.AppendLine($@"</div>");
                            }
                        }
                        else
                        {
                            sb.AppendLine(@"<div class=""text-xs text-gray-500 italic"">No external stores or pylons loaded.</div>");
                        }

                        sb.AppendLine($@"</div>");
                    }
                    sb.AppendLine(@"</div></details>");
                }
            }
            return sb.ToString();
        }

        public List<(string Coalition, string GroupName, string Task, string AircraftType, int UnitsCount, double StartTimeSec, List<(string UnitName, List<(int Pylon, string Weapon)> Pylons, double Flare, double Chaff, double Gun)> Units)> ExtractAtoWithLoadouts(Table mission)
        {
            var atoList = new List<(string, string, string, string, int, double, List<(string, List<(int, string)>, double, double, double)>)>();
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return atoList;

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

                    foreach (var category in new[] { "plane", "helicopter", "ship" })
                    {
                        var catVal = country.Get(category);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var groupPair in groupListVal.Table.Pairs)
                        {
                            if (groupPair.Value.Type != DataType.Table) continue;
                            var group = groupPair.Value.Table;

                            string groupName = group.Get("name")?.String ?? "Unknown";
                            string task = group.Get("task")?.String ?? "None";
                            string aircraftType = "Unknown";
                            int unitsCount = 0;
                            double startTime = group.Get("start_time")?.Number ?? 0;

                            var groupUnits = new List<(string UnitName, List<(int Pylon, string Weapon)> Pylons, double Flare, double Chaff, double Gun)>();

                            var unitsVal = group.Get("units");
                            if (unitsVal.Type == DataType.Table && unitsVal.Table.Length > 0)
                            {
                                unitsCount = unitsVal.Table.Length;
                                var firstUnit = unitsVal.Table.Get(1)?.Table;
                                if (firstUnit != null) aircraftType = firstUnit.Get("type")?.String ?? "Unknown";

                                foreach (var uPair in unitsVal.Table.Pairs)
                                {
                                    if (uPair.Value.Type != DataType.Table) continue;
                                    var unitTable = uPair.Value.Table;
                                    string uName = unitTable.Get("name")?.String ?? "Unknown Unit";
                                    var pylonsList = new List<(int Pylon, string Weapon)>();
                                    double flare = 0, chaff = 0, gun = 0;

                                    var payloadVal = unitTable.Get("payload");
                                    if (payloadVal?.Type == DataType.Table)
                                    {
                                        flare = payloadVal.Table.Get("flare")?.Number ?? 0;
                                        chaff = payloadVal.Table.Get("chaff")?.Number ?? 0;
                                        gun = payloadVal.Table.Get("gun_100")?.Number ?? payloadVal.Table.Get("gun")?.Number ?? 0;

                                        var pylonsVal = payloadVal.Table.Get("pylons");
                                        if (pylonsVal?.Type == DataType.Table)
                                        {
                                            foreach (var pyl in pylonsVal.Table.Pairs)
                                            {
                                                if (pyl.Key.Type != DataType.Number) continue;
                                                int pylonNum = (int)pyl.Key.Number;

                                                if (pyl.Value.Type != DataType.Table) continue;
                                                string clsid = pyl.Value.Table.Get("CLSID")?.String ?? pyl.Value.Table.Get("clsid")?.String ?? "";

                                                if (!string.IsNullOrEmpty(clsid))
                                                {
                                                    string weaponName = weaponService.GetWeaponName(clsid);
                                                    pylonsList.Add((pylonNum, weaponName));
                                                }
                                            }
                                        }
                                    }
                                    pylonsList = pylonsList.OrderBy(p => p.Pylon).ToList();
                                    groupUnits.Add((uName, pylonsList, flare, chaff, gun));
                                }
                            }
                            atoList.Add((side, groupName, task, aircraftType, unitsCount, startTime, groupUnits));
                        }
                    }
                }
            }
            return atoList;
        }

        // ==========================================
        // REQUIRED MODS EXTRACTION & GENERATION
        // ==========================================

        private static string GenerateRequiredModsHtmlSection(Table mission)
        {
            var sb = new StringBuilder();
            var reqVal = mission.Get("requiredModules");

            bool hasMods = false;
            List<string> mods = new();

            if (reqVal.Type == DataType.Table)
            {
                foreach (var pair in reqVal.Table.Pairs)
                {
                    string modName = pair.Key?.ToString()?.Trim('"', ' ') ?? "";
                    if (!string.IsNullOrWhiteSpace(modName)) mods.Add(modName);
                }
                hasMods = mods.Count > 0;
            }

            if (hasMods)
            {
                sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6 border-b border-gray-700 pb-2"">🧩 Required Mods</h2>");
                sb.AppendLine(@"<div class=""bg-amber-900/20 border border-amber-700/50 rounded-2xl p-6"">");
                sb.AppendLine(@"<p class=""font-semibold text-amber-500 mb-4 flex items-center gap-2""><span class=""text-2xl"">⚠️</span> This mission requires the following modules/mods to load correctly:</p><ul class=""grid grid-cols-1 md:grid-cols-2 gap-2"">");
                foreach (var mod in mods.OrderBy(m => m))
                {
                    sb.AppendLine($@"<li class=""flex items-center gap-3 bg-amber-950/30 px-4 py-2 rounded-lg text-amber-200 border border-amber-800/30""><span class=""text-amber-600 font-bold"">·</span> {mod}</li>");
                }
                sb.AppendLine(@"</ul></div>");
            }
            else
            {
                sb.AppendLine(@"<div class=""mt-16 p-4 rounded-xl bg-gray-900 border border-gray-800 text-gray-500 italic text-center"">No additional mods required for this mission.</div>");
            }
            return sb.ToString();
        }

        // ==========================================
        // WEATHER EXTRACTION & GENERATION
        // ==========================================
        public static WeatherData GetWeatherData(Table mission, UnitsSystem units)
        {
            var w = mission.Get("weather")?.Table ?? new Table(new Script());
            var clouds = w.Get("clouds")?.Table;
            var wind = w.Get("wind")?.Table;
            var visibility = w.Get("visibility")?.Table;
            var season = w.Get("season")?.Table;

            string cloudStr = clouds?.Get("preset")?.String ?? "Clear";
            double baseM = clouds?.Get("base")?.Number ?? 0;
            double thickM = clouds?.Get("thickness")?.Number ?? 0;

            string cloudLine = baseM > 0
                ? $"Base {(units == UnitsSystem.Metric ? baseM / 1000 : baseM * 3.28084):F1} {(units == UnitsSystem.Metric ? "km" : "ft")} • Thickness {(units == UnitsSystem.Metric ? thickM / 1000 : thickM * 3.28084):F1} {(units == UnitsSystem.Metric ? "km" : "ft")}"
                : "";

            return new WeatherData(
                cloudStr,
                GetWindString(wind?.Get("atGround"), units),
                GetWindString(wind?.Get("at2000"), units),
                GetWindString(wind?.Get("at8000"), units),
                GetVisibilityString(visibility?.Get("distance")?.Number ?? 80000, units),
                units == UnitsSystem.Metric ? (w.Get("qnh")?.Number ?? 760) : (w.Get("qnh")?.Number ?? 760) * 0.02953,
                units == UnitsSystem.Metric ? (season?.Get("temperature")?.Number ?? 15) : ((season?.Get("temperature")?.Number ?? 15) * 9.0 / 5) + 32,
                cloudLine
            );
        }

        internal static string GetWindString(DynValue windDyn, UnitsSystem units)
        {
            if (windDyn?.Type != DataType.Table) return "—";
            var t = windDyn.Table;
            double speedMs = t.Get("speed")?.Number ?? 0;
            double dir = t.Get("dir")?.Number ?? 0;

            return units == UnitsSystem.Metric ? $"{(int)dir}° / {speedMs:F1} m/s" : $"{(int)dir}° / {(speedMs * 1.94384):F0} kt";
        }

        internal static string GetVisibilityString(double meters, UnitsSystem units)
        {
            if (meters >= 80000) return "Unlimited";
            return units == UnitsSystem.Metric ? $"{meters / 1000:F0} km" : $"{(meters * 0.000621371):F0} mi";
        }

        private static string GenerateWeatherHtmlSection(Table mission, UnitsSystem units)
        {
            var weatherVal = mission.Get("weather");
            if (weatherVal.Type != DataType.Table) return "<p class=\"text-yellow-400\">No weather data found.</p>";

            var data = GetWeatherData(mission, units);
            var sb = new StringBuilder();

            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">🌤️ Weather</h2><div class=""grid grid-cols-2 md:grid-cols-3 gap-6 text-sm"">");
            sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-5""><div class=""text-gray-400 text-xs mb-1"">CLOUDS</div><div class=""text-2xl font-semibold"">{data.Clouds}</div>");
            if (!string.IsNullOrEmpty(data.CloudLine)) sb.AppendLine($@"<div class=""text-xs text-gray-400"">{data.CloudLine}</div>");
            sb.AppendLine("</div>");

            sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-5""><div class=""text-gray-400 text-xs mb-1"">WIND</div><div>Surface: <span class=""font-semibold"">{data.WindSurface}</span></div><div>2000 ft: <span class=""font-semibold"">{data.Wind2000}</span></div><div>8000 ft: <span class=""font-semibold"">{data.Wind8000}</span></div></div>");

            string pressureUnit = units == UnitsSystem.Metric ? "hPa" : "inHg";
            string tempUnit = units == UnitsSystem.Metric ? "°C" : "°F";
            sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-5""><div class=""text-gray-400 text-xs mb-1"">VISIBILITY / QNH</div><div>Visibility: <span class=""font-semibold"">{data.Visibility}</span></div><div>QNH: <span class=""font-semibold"">{data.Qnh:F2} {pressureUnit}</span></div><div>Temperature: <span class=""font-semibold"">{data.Temp:F0} {tempUnit}</span></div></div>");

            sb.AppendLine("</div>");
            return sb.ToString();
        }

        // ==========================================
        // ORDER OF BATTLE EXTRACTION & GENERATION
        // ==========================================
        public static List<OrderOfBattleSide> ExtractOrderOfBattle(Table mission)
        {
            var oobList = new List<OrderOfBattleSide>();
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return oobList;

            var coalition = coalitionVal.Table;
            string[] sides = { "blue", "red", "neutral" };

            foreach (var side in sides)
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                var aircraftCounts = new Dictionary<string, int>();
                int totalAircraft = 0, totalShips = 0, totalGround = 0, totalStatics = 0;

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

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

                                aircraftCounts[type] = aircraftCounts.GetValueOrDefault(type) + 1;
                                totalAircraft++;
                            }
                        }
                    }

                    var shipVal = country.Get("ship");
                    if (shipVal.Type == DataType.Table)
                    {
                        var shipGroups = shipVal.Table.Get("group");
                        if (shipGroups.Type == DataType.Table) totalShips += shipGroups.Table.Length;
                    }

                    var vehicleVal = country.Get("vehicle");
                    if (vehicleVal.Type == DataType.Table)
                    {
                        var vehicleGroups = vehicleVal.Table.Get("group");
                        if (vehicleGroups.Type == DataType.Table) totalGround += vehicleGroups.Table.Length;
                    }

                    var staticVal = country.Get("static");
                    if (staticVal.Type == DataType.Table)
                    {
                        var staticGroups = staticVal.Table.Get("group");
                        if (staticGroups.Type == DataType.Table) totalStatics += staticGroups.Table.Length;
                    }
                }
                oobList.Add(new OrderOfBattleSide(side, totalAircraft, totalShips, totalGround, totalStatics, aircraftCounts));
            }
            return oobList;
        }

        private static string GenerateOrderOfBattleHtmlSection(Table mission)
        {
            var oobList = ExtractOrderOfBattle(mission);
            if (oobList.Count == 0) return "<p class=\"text-yellow-400\">No coalition data found for OOB.</p>";

            var sb = new StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">📊 Order of Battle (OOB)</h2>");

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };

            for (int i = 0; i < sides.Length; i++)
            {
                var sideData = oobList.FirstOrDefault(o => o.Coalition == sides[i]);
                if (sideData == null) continue;

                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{sides[i]}-400"">{sideNames[i]} Coalition</h3>");
                sb.AppendLine(@"<div class=""grid grid-cols-5 gap-4 mb-6 text-center"">");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">AIRCRAFT</div><div class=""text-3xl font-bold"">{sideData.TotalAircraft}</div></div>");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">SHIPS</div><div class=""text-3xl font-bold"">{sideData.TotalShips}</div></div>");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">GROUND</div><div class=""text-3xl font-bold"">{sideData.TotalGround}</div></div>");
                sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4""><div class=""text-xs text-gray-400"">STATICS</div><div class=""text-3xl font-bold"">{sideData.TotalStatics}</div></div>");
                sb.AppendLine("</div>");

                if (sideData.AircraftBreakdown.Count > 0)
                {
                    sb.AppendLine(@"<details class=""mb-8""><summary class=""cursor-pointer text-lg font-medium mb-2"">Aircraft Breakdown</summary><table class=""w-full border-collapse text-sm""><thead><tr class=""bg-gray-800""><th class=""p-3 text-left"">Type</th><th class=""p-3 text-center"">Count</th></tr></thead><tbody>");
                    foreach (var kvp in sideData.AircraftBreakdown.OrderByDescending(k => k.Value))
                        sb.AppendLine($@"<tr class=""border-t border-gray-700""><td class=""p-3"">{kvp.Key}</td><td class=""p-3 text-center font-semibold"">{kvp.Value}</td></tr>");
                    sb.AppendLine("</tbody></table></details>");
                }
            }
            return sb.ToString();
        }

        // ==========================================
        // UNITS & TARGETS EXTRACTION & GENERATION
        // ==========================================
        public static List<UnitTargetGroup> ExtractUnitsAndTargets(Table mission, Dictionary<string, ThreatData> threatDict)
        {
            var targets = new List<UnitTargetGroup>();
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return targets;

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
                            double maxDet = 0;
                            double maxThr = 0;

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

                            // BATCH OPTIMIZATION: Only canonicalize unique unit types
                            foreach (var uType in unitCounts.Keys)
                            {
                                string canonicalKey = JsonThreatDatabaseService.Canonicalize(uType);
                                if (threatDict != null && threatDict.TryGetValue(canonicalKey, out var tData))
                                {
                                    maxDet = Math.Max(maxDet, tData.DetectionRange);
                                    maxThr = Math.Max(maxThr, tData.ThreatRange);
                                }
                            }

                            int totalUnits = unitCounts.Values.Sum();
                            var unitList = unitCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ×{kv.Value}").ToList();
                            string unitInfo = unitList.Count > 0 ? string.Join(", ", unitList) : "No units listed";

                            targets.Add(new UnitTargetGroup(side, category, groupName, gx, gy, totalUnits, unitInfo, maxDet, maxThr));
                        }
                    }
                }
            }
            return targets;
        }

        private static string GenerateUnitsAndTargetsHtmlSection(Table mission, Dictionary<string, ThreatData> threatDict)
        {
            var targets = ExtractUnitsAndTargets(mission, threatDict);
            var sb = new StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">📦 Units & Targets</h2>");

            if (targets.Count == 0) return sb.AppendLine(@"<p class=""text-emerald-400 italic"">No ground, sea, or static units found in this mission.</p>").ToString();

            string[] sides = { "blue", "red", "neutral" };
            string[] sideEmojis = { "🔵", "🔴", "⚪" };
            string[] sideNames = { "BLUE COALITION", "RED COALITION", "NEUTRAL COALITION" };

            for (int i = 0; i < sides.Length; i++)
            {
                var sideTargets = targets.Where(t => t.Coalition == sides[i]).ToList();
                if (sideTargets.Count == 0) continue;

                string colorClass = sides[i] == "blue" ? "blue" : sides[i] == "red" ? "red" : "slate";
                sb.AppendLine($@"<h3 class=""text-{colorClass}-400 text-lg font-semibold mt-10 mb-5 flex items-center gap-2""><span class=""text-2xl"">{sideEmojis[i]}</span> {sideNames[i]}</h3>");

                foreach (var target in sideTargets)
                {
                    string icon = target.Category == "ship" ? "⚓" : target.Category == "vehicle" ? "🪖" : "📦";

                    // Format Ranges
                    string detStr = target.MaxDet > 0 ? $"👁️ {(target.MaxDet / 1000):F0}km" : "";
                    string thrStr = target.MaxThr > 0 ? $"⚔️ {(target.MaxThr / 1000):F0}km" : "";

                    sb.AppendLine($@"<div class=""flex items-start gap-6 p-6 bg-slate-800 border border-slate-700 rounded-3xl hover:border-{colorClass}-500 transition-all"">
                <div class=""text-5xl flex-shrink-0 mt-1"">{icon}</div>
                <div class=""flex-1"">
                    <div class=""font-semibold text-slate-100 text-lg"">{target.GroupName}</div>
                    <div class=""text-xs font-mono text-slate-400 mt-1"">{target.X:F0}, {target.Y:F0}</div>
                    <div class=""text-sm text-slate-300 mt-3"">{target.UnitInfo}</div>
                    
                    <div class=""flex gap-2 mt-3"">
                        {(string.IsNullOrEmpty(detStr) ? "" : $@"<span class=""px-2 py-0.5 rounded bg-yellow-900/50 text-yellow-200 text-xs font-mono border border-yellow-700"">{detStr}</span>")}
                        {(string.IsNullOrEmpty(thrStr) ? "" : $@"<span class=""px-2 py-0.5 rounded bg-red-900/50 text-red-200 text-xs font-mono border border-red-700"">{thrStr}</span>")}
                    </div>
                </div>
                <div class=""text-right text-xs font-medium text-slate-400"">{target.TotalUnits}<br/><span class=""text-[10px]"">UNITS</span></div>
            </div>");
                }
            }
            return sb.ToString();
        }

        // ==========================================
        // PLAYER SLOTS EXTRACTION & GENERATION
        // ==========================================
        public static List<PlayerSlotGroup> ExtractPlayerSlots(Table mission)
        {
            var slots = new List<PlayerSlotGroup>();
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return slots;

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
                                    if (aircraftType == "Unknown") aircraftType = unit.Get("type")?.String ?? "Unknown";
                                }
                            }

                            if (clientCount > 0)
                            {
                                slots.Add(new PlayerSlotGroup(side, group.Get("name")?.String ?? "Unknown", aircraftType, group.Get("task")?.String ?? "None", clientCount));
                            }
                        }
                    }
                }
            }
            return slots;
        }

        private static string GeneratePlayerSlotsHtmlSection(Table mission)
        {
            var slots = ExtractPlayerSlots(mission);
            if (slots.Count == 0) return "<p class=\"text-yellow-400\">No player slot data found.</p>";

            var sb = new StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">🧑‍✈️ Player &amp; Client Spawn Spots</h2>");

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };

            for (int i = 0; i < sides.Length; i++)
            {
                var sideSlots = slots.Where(s => s.Coalition == sides[i]).ToList();
                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{sides[i]}-400"">{sideNames[i]} Coalition</h3>");

                if (sideSlots.Count > 0)
                {
                    foreach (var slot in sideSlots)
                    {
                        sb.AppendLine($@"<div class=""bg-gray-900 rounded-xl p-4 mb-4""><div class=""flex justify-between""><div><span class=""font-semibold"">{slot.GroupName}</span> — {slot.AircraftType}</div><div class=""text-sm text-gray-400"">{slot.Task} • {slot.ClientCount} client slot{(slot.ClientCount > 1 ? "s" : "")}</div></div></div>");
                    }
                }
                else
                {
                    sb.AppendLine(@"<p class=""text-gray-400 italic"">No player/client slots found for this coalition.</p>");
                }
            }
            return sb.ToString();
        }

        // ==========================================
        // FLIGHTS & WAYPOINTS EXTRACTION
        // ==========================================

        internal static SvgMapData CalculateSvgMapData(List<(double x, double y, double alt, double speed, string action, string name, List<(double x, double y, string targetName)> targets)> waypoints)
        {
            if (waypoints == null || waypoints.Count == 0) return new SvgMapData([], [], []);

            double minX = waypoints.Min(p => p.x);
            double maxX = waypoints.Max(p => p.x);
            double minY = waypoints.Min(p => p.y);
            double maxY = waypoints.Max(p => p.y);

            double rangeX = Math.Max(maxX - minX, 100);
            double rangeY = Math.Max(maxY - minY, 100);
            double scale = Math.Max(rangeX, rangeY);

            double padding = scale * 0.15;
            double effectiveScale = scale + (padding * 2);

            double ProjectX(double y) => (y - minY + padding) / effectiveScale * 800.0;
            double ProjectY(double x) => 500.0 - ((x - minX + padding) / effectiveScale * 500.0);

            var routePoints = waypoints.Select(wp => new SvgPoint(ProjectX(wp.y), ProjectY(wp.x))).ToList();

            var targetLines = new List<SvgTarget>();
            foreach (var wp in waypoints)
            {
                var source = new SvgPoint(ProjectX(wp.y), ProjectY(wp.x));
                foreach (var t in wp.targets)
                {
                    targetLines.Add(new SvgTarget(source, new SvgPoint(ProjectX(t.y), ProjectY(t.x)), t.targetName));
                }
            }

            var markers = new List<SvgWaypointMarker>();
            var grouped = waypoints.GroupBy(wp => $"{Math.Round(wp.x / 500)},{Math.Round(wp.y / 500)}");

            foreach (var group in grouped)
            {
                var first = group.First();
                var pt = new SvgPoint(ProjectX(first.y), ProjectY(first.x));
                bool isCluster = group.Count() > 1;

                string tooltip = string.Join("&#10;", group.Select(g => $"{g.name}: {g.action}"));
                string label = isCluster ? $"{group.Count()} pts" : first.name;

                markers.Add(new SvgWaypointMarker(pt, isCluster, tooltip, label));
            }

            return new SvgMapData(routePoints, targetLines, markers);
        }

        public static List<FlightWaypointGroup> ExtractFlightWaypoints(Table mission)
        {
            var flights = new List<FlightWaypointGroup>();
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return flights;

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

                            var routeVal = group.Get("route");
                            if (routeVal.Type != DataType.Table) continue;

                            var pointsVal = routeVal.Table.Get("points");
                            if (pointsVal.Type != DataType.Table || pointsVal.Table.Length == 0) continue;

                            string groupName = group.Get("name")?.String ?? "Unknown";
                            string task = group.Get("task")?.String ?? "None";

                            var unitsVal = group.Get("units");
                            int unitCount = unitsVal?.Table?.Length ?? 0;

                            string aircraft = "Unknown";
                            if (unitCount > 0) aircraft = unitsVal.Table.Get(1)?.Table?.Get("type")?.String ?? "Unknown";

                            flights.Add(new FlightWaypointGroup(side, groupName, aircraft, unitCount, task, pointsVal.Table));
                        }
                    }
                }
            }
            return flights;
        }

        public static List<(double x, double y, string unitName, string unitType, string groupName, string canonicalKey)> ExtractAllGroundUnits(Table mission)
        {
            var units = new List<(double x, double y, string unitName, string unitType, string groupName, string canonicalKey)>();
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return units;

            string[] sides = { "blue", "red", "neutral" };

            foreach (var side in sides)
            {
                var sideVal = coalitionVal.Table.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

                    foreach (var cat in new[] { "vehicle", "static" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var groupPair in groupListVal.Table.Pairs)
                        {
                            if (groupPair.Value.Type != DataType.Table) continue;
                            var group = groupPair.Value.Table;

                            string groupName = group.Get("name")?.String ?? "";

                            var unitsVal = group.Get("units");
                            if (unitsVal.Type != DataType.Table) continue;

                            foreach (var unitPair in unitsVal.Table.Pairs)
                            {
                                if (unitPair.Value.Type != DataType.Table) continue;
                                var unit = unitPair.Value.Table;

                                double x = unit.Get("x")?.Number ?? 0;
                                double y = unit.Get("y")?.Number ?? 0;
                                string unitName = unit.Get("name")?.String ?? "";
                                string unitType = unit.Get("type")?.String ?? "Unknown";

                                // BATCH OPTIMIZATION: Do this once per unit
                                string canonicalKey = JsonThreatDatabaseService.Canonicalize(unitType);
                                units.Add((x, y, unitName, unitType, groupName, canonicalKey));
                            }
                        }
                    }
                }
            }
            return units;
        }

        private string GenerateFlightsWithWaypointsHtmlSection(Table mission, MissionIndexer indexer, Dictionary<string, ThreatData> threatDict)
        {
            var flights = ExtractFlightWaypoints(mission);
            if (flights.Count == 0) return "<p class=\"text-yellow-400\">No flight waypoint data found.</p>";

            var allGroundUnits = ExtractAllGroundUnits(mission);

            var sb = new StringBuilder();
            sb.AppendLine(@"<h2 class=""text-2xl font-semibold mt-16 mb-6"">🛫 Flights &amp; Waypoints</h2>");

            string[] sides = { "blue", "red", "neutral" };
            string[] sideNames = { "Blue", "Red", "Neutral" };

            for (int i = 0; i < sides.Length; i++)
            {
                var sideFlights = flights.Where(f => f.Coalition == sides[i]).ToList();
                if (sideFlights.Count == 0) continue;

                sb.AppendLine($@"<h3 class=""text-xl font-semibold mt-8 mb-4 text-{sides[i]}-400"">{sideNames[i]} Coalition</h3>");

                foreach (var flight in sideFlights)
                {
                    sb.AppendLine($@"<details class=""mb-8 bg-gray-900 rounded-2xl p-5""><summary class=""cursor-pointer font-medium text-lg flex items-center gap-3"">✈️ {flight.GroupName} — {flight.Aircraft} ({flight.UnitCount}×) • {flight.Task}</summary>");
                    sb.AppendLine(@"<div class=""mt-4""><table class=""w-full border-collapse text-xs""><thead><tr class=""bg-gray-800""><th class=""p-2 text-center"" style=""width: 5%"">#</th><th class=""p-2 text-left"" style=""width: 25%"">Action</th><th class=""p-2 text-left"" style=""width: 25%"">Tasking</th><th class=""p-2 text-right"" style=""width: 12%"">Alt (ft)</th><th class=""p-2 text-right"" style=""width: 12%"">Speed (kt)</th><th class=""p-2 text-left"" style=""width: 21%"">DCS (x, y)</th></tr></thead><tbody>");

                    var waypoints = ParseWaypoints(flight.RoutePoints, indexer);
                    int idx = 1;

                    foreach (var wp in waypoints)
                    {
                        string tasking = wp.targets.Count == 0
                            ? "—"
                            : string.Join("<br/>", wp.targets.Select(t => System.Net.WebUtility.HtmlEncode(t.targetName)));

                        sb.AppendLine($@"<tr class=""border-t border-gray-700""><td class=""p-2 text-center font-semibold"">{idx}</td><td class=""p-2 text-left"">{wp.action}</td><td class=""p-2 text-left text-amber-200"">{tasking}</td><td class=""p-2 text-right"">{(int)(wp.alt * 3.28084)}</td><td class=""p-2 text-right"">{(int)(wp.speed * 1.94384)}</td><td class=""p-2 text-left font-mono"">{wp.x:F0}, {wp.y:F0}</td></tr>");
                        idx++;
                    }
                    sb.AppendLine("</tbody></table></div>");

                    if (waypoints.Count > 1) sb.AppendLine(GenerateFlightSvgMap(waypoints, allGroundUnits, flight.GroupName, threatDict));
                    sb.AppendLine("</details>");
                }
            }
            return sb.ToString();
        }

        private static string GenerateFlightSvgMap(
            List<(double x, double y, double alt, double speed, string action, string name, List<(double x, double y, string targetName)> targets)> waypoints,
            List<(double x, double y, string unitName, string unitType, string groupName, string canonicalKey)> staticThreats,
            string groupName,
            Dictionary<string, ThreatData> threatDict)
        {
            var data = CalculateSvgMapData(waypoints);
            if (data.RoutePoints.Count == 0) return "";

            double minX = waypoints.Min(p => p.x);
            double maxX = waypoints.Max(p => p.x);
            double minY = waypoints.Min(p => p.y);
            double maxY = waypoints.Max(p => p.y);

            double rangeX = Math.Max(maxX - minX, 10000);
            double rangeY = Math.Max(maxY - minY, 10000);
            double scale = Math.Max(rangeX, rangeY);
            double padding = scale * 0.25;
            double effectiveScale = scale + (padding * 2);

            double ProjectX(double y) => (y - minY + padding) / effectiveScale * 800.0;
            double ProjectY(double x) => 500.0 - ((x - minX + padding) / effectiveScale * 500.0);

            var sb = new StringBuilder();
            sb.AppendLine($@"<details class=""mt-6""><summary class=""cursor-pointer text-sm font-medium mb-2"">🗺️ Interactive Route Map for {groupName}</summary>");
            sb.AppendLine($@"<svg width=""800"" height=""500"" viewBox=""0 0 800 500"" class=""border border-gray-700 rounded-xl bg-gray-950"">");

            sb.Append(@"<polyline points=""");
            foreach (var pt in data.RoutePoints) sb.Append($"{pt.X:F0},{pt.Y:F0} ");
            sb.AppendLine(@""" fill=""none"" stroke=""#22d3ee"" stroke-width=""4"" />");

            foreach (var t in data.Targets)
            {
                sb.AppendLine($@"<g><line x1=""{t.Target.X - 6:F0}"" y1=""{t.Target.Y - 6:F0}"" x2=""{t.Target.X + 6:F0}"" y2=""{t.Target.Y + 6:F0}"" stroke=""red"" stroke-width=""2""/><line x1=""{t.Target.X + 6:F0}"" y1=""{t.Target.Y - 6:F0}"" x2=""{t.Target.X - 6:F0}"" y2=""{t.Target.Y + 6:F0}"" stroke=""red"" stroke-width=""2""/>
                    <text x=""{t.Target.X + 8:F0}"" y=""{t.Target.Y - 8:F0}"" fill=""red"" font-size=""10"" font-family=""monospace"" font-weight=""bold"">{t.TargetName}</text></g>");
            }

            if (staticThreats != null)
            {
                var drawnLabels = new List<(double x, double y)>();
                var printedGroups = new HashSet<string>();

                foreach (var threat in staticThreats)
                {
                    double px = ProjectX(threat.y);
                    double py = ProjectY(threat.x);

                    if (px >= -4000 && px <= 4800 && py >= -4000 && py <= 4500)
                    {
                        sb.AppendLine("<g>");

                        string displayName = threat.unitType;

                        // BATCH OPTIMIZATION: Use the pre-calculated key
                        if (threatDict != null && threatDict.TryGetValue(threat.canonicalKey, out var threatData))
                        {
                            displayName = !string.IsNullOrWhiteSpace(threatData.DisplayName) ? threatData.DisplayName : threat.unitType;

                            double detRadius = threatData.DetectionRange > 0 ? (threatData.DetectionRange / effectiveScale) * 800.0 : 0;
                            double thrRadius = threatData.ThreatRange > 0 ? (threatData.ThreatRange / effectiveScale) * 800.0 : 0;

                            if (detRadius > 0)
                            {
                                sb.AppendLine($@"<circle cx=""{px:F0}"" cy=""{py:F0}"" r=""{Math.Max(detRadius, 5):F0}"" fill=""rgba(234, 179, 8, 0.05)"" stroke=""rgba(234, 179, 8, 0.5)"" stroke-width=""1"" stroke-dasharray=""2 4""/>");
                            }
                            if (thrRadius > 0)
                            {
                                sb.AppendLine($@"<circle cx=""{px:F0}"" cy=""{py:F0}"" r=""{Math.Max(thrRadius, 5):F0}"" fill=""rgba(220, 38, 38, 0.1)"" stroke=""rgba(220, 38, 38, 0.6)"" stroke-width=""1"" stroke-dasharray=""4 4""/>");
                            }
                        }

                        if (px >= -20 && px <= 820 && py >= -20 && py <= 520)
                        {
                            string hoverLabel = displayName;
                            if (!string.IsNullOrWhiteSpace(threat.unitName) && !threat.unitName.StartsWith("DictKey_")) hoverLabel = threat.unitName;
                            if (!string.IsNullOrWhiteSpace(threat.groupName) && !threat.groupName.StartsWith("DictKey_")) hoverLabel = $"{threat.groupName} ({hoverLabel})";

                            string visibleLabel = "";
                            bool hasGroupName = !string.IsNullOrWhiteSpace(threat.groupName) && !threat.groupName.StartsWith("DictKey_");
                            bool hasUnitName = !string.IsNullOrWhiteSpace(threat.unitName) && !threat.unitName.StartsWith("DictKey_");

                            if (hasGroupName)
                            {
                                if (!printedGroups.Contains(threat.groupName))
                                {
                                    visibleLabel = threat.groupName;
                                    printedGroups.Add(threat.groupName);
                                }
                            }
                            else if (hasUnitName)
                            {
                                visibleLabel = threat.unitName;
                            }
                            else
                            {
                                visibleLabel = displayName;
                            }

                            double labelX = px + 8;
                            double labelY = py + 4;

                            if (!string.IsNullOrEmpty(visibleLabel))
                            {
                                int attempts = 0;
                                // BATCH OPTIMIZATION: Cap the infinite loop lockup!
                                while (attempts < 8 && drawnLabels.Any(l => Math.Abs(l.x - labelX) < 100 && Math.Abs(l.y - labelY) < 12))
                                {
                                    labelY += 12;
                                    attempts++;
                                }
                                drawnLabels.Add((labelX, labelY));
                            }

                            sb.AppendLine($@"<circle cx=""{px:F0}"" cy=""{py:F0}"" r=""4"" fill=""red"" stroke=""white"" stroke-width=""1""><title>{hoverLabel}</title></circle>");

                            if (!string.IsNullOrEmpty(visibleLabel))
                            {
                                sb.AppendLine($@"<text x=""{labelX:F0}"" y=""{labelY:F0}"" fill=""#fca5a5"" font-size=""10"" font-family=""monospace"" pointer-events=""none"">{visibleLabel}</text>");
                            }
                        }
                        sb.AppendLine("</g>");
                    }
                }
            }

            foreach (var m in data.Markers)
            {
                string color = m.IsCluster ? "#f59e0b" : "#22d3ee";
                string textColor = m.IsCluster ? "#f59e0b" : "#67e8f9";
                sb.AppendLine($@"<g class=""cursor-pointer""><circle cx=""{m.Point.X:F0}"" cy=""{m.Point.Y:F0}"" r=""8"" fill=""{color}""><title>{m.Tooltip}</title></circle>
               <text x=""{m.Point.X + 12:F0}"" y=""{m.Point.Y + 5:F0}"" fill=""{textColor}"" font-size=""11"" font-family=""monospace"" pointer-events=""none"">{m.Label}</text></g>");
            }

            sb.AppendLine("</svg></details>");
            return sb.ToString();
        }

        private static string GetTaskDisplayName(string taskId)
        {
            return taskId switch
            {
                "AttackUnit" => "Attack Unit",
                "AttackGroup" => "Attack Group",
                "EngageTargets" => "Engage Targets",
                _ => taskId
            };
        }

        // ==========================================
        // PARSING & MATH HELPERS
        // ==========================================
        public static List<(double x, double y, string name)> ProcessTargets(Table tasks, MissionIndexer indexer, string wpName)
        {
            var targets = new List<(double x, double y, string name)>();

            foreach (var t in tasks.Pairs)
            {
                if (t.Value.Type != DataType.Table) continue;

                var currentTask = t.Value.Table;
                var p = currentTask.Get("params")?.Table;
                if (p == null) continue;

                string taskName = GetTaskDisplayName(currentTask.Get("id")?.String ?? "Task");

                string targetName = "Target";
                bool foundTarget = false;
                double targetX = p.Get("x")?.Number ?? 0;
                double targetY = p.Get("y")?.Number ?? 0;

                double groupId = p.Get("groupId")?.Number ?? 0;
                if (groupId > 0)
                {
                    targetName = $"{taskName}: {indexer.ResolveNameFromGroupId(groupId)}";

                    if (targetX == 0 && targetY == 0 && indexer.GroupsById.TryGetValue(groupId, out var groupTable))
                    {
                        var units = groupTable.Get("units")?.Table;
                        if (units != null && units.Length > 0)
                        {
                            var firstUnit = units.Get(1)?.Table;
                            targetX = firstUnit?.Get("x")?.Number ?? 0;
                            targetY = firstUnit?.Get("y")?.Number ?? 0;
                        }
                    }

                    foundTarget = true;
                }
                else
                {
                    double unitId = p.Get("unitId")?.Number ?? 0;
                    if (unitId > 0)
                    {
                        targetName = $"{taskName}: {indexer.ResolveNameFromUnitId(unitId)}";

                        if (targetX == 0 && targetY == 0)
                        {
                            indexer.TryGetUnitPosition(unitId, out targetX, out targetY);
                        }

                        foundTarget = true;
                    }
                }

                if (!foundTarget && (targetX != 0 || targetY != 0))
                {
                    string unitType = indexer.FindUnitTypeAtLocation(targetX, targetY);
                    targetName = !string.IsNullOrEmpty(unitType)
                        ? $"{taskName}: {unitType}"
                        : $"{taskName}: {wpName}";

                    foundTarget = true;
                }

                if (foundTarget && (targetX != 0 || targetY != 0))
                {
                    targets.Add((targetX, targetY, targetName));
                }
            }

            return targets;
        }

        public static List<(double x, double y, double alt, double speed, string action, string name, List<(double x, double y, string targetName)> targets)> ParseWaypoints(Table pointsVal, MissionIndexer indexer)
        {
            var waypoints = new List<(double x, double y, double alt, double speed, string action, string name, List<(double x, double y, string targetName)> targets)>();
            int idx = 1;

            foreach (var pPair in pointsVal.Pairs)
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
                    if (tasks != null) waypointTargets.AddRange(ProcessTargets(tasks, indexer, wpName));
                }

                waypoints.Add((x, y, alt, speed, action, wpName, waypointTargets));
                idx++;
            }
            return waypoints;
        }
    }
}