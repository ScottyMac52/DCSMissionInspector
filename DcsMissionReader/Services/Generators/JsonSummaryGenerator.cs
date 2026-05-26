using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using System.Text.Json;

namespace DcsMissionReader.Services.Generators
{
    public class JsonSummaryGenerator : IMissionExportStrategy
    {
        public bool ShouldExport(AppOptions options) => options.CreateJson || options.FullExport;

        public void Export(MissionContext context)
        {
            // 1. Gather Metadata for Summary
            string imagesDir = Path.Combine(context.ReportDir, "images");
            string kneeboardsDir = Path.Combine(context.ReportDir, "kneeboards");
            string mapName = File.Exists(Path.Combine(context.TempDir, "theatre")) ? File.ReadAllText(Path.Combine(context.TempDir, "theatre")).Trim() : "Unknown";

            // Re-resolve metadata
            var dateTable = context.MissionTable.Get("date").Table;
            string fullDate = $"{(int)dateTable.Get("Year").Number}-{(int)dateTable.Get("Month").Number:D2}-{(int)dateTable.Get("Day").Number:D2}";
            double startSec = context.MissionTable.Get("start_time").Number;
            string startTime = $"{(int)(startSec / 3600):D2}:{(int)((startSec % 3600) / 60):D2}";
            string version = context.MissionTable.Get("version").ToString();
            // We need a dictTable for Resolve if it exists
            Table? dictTable = MissionUtils.LoadDictionary(context.TempDir);
            string description = MissionUtils.Resolve(context.MissionTable.Get("descriptionText"), dictTable);
            string blueTask = MissionUtils.Resolve(context.MissionTable.Get("descriptionBlueTask"), dictTable);
            string redTask = MissionUtils.Resolve(context.MissionTable.Get("descriptionRedTask"), dictTable);

            // 2. Export Actions
            if (context.Options.CreateJson)
            {
                GenerateJsonSummary(context.ReportDir, context.Sortie, mapName, fullDate, startTime, version, description, blueTask, redTask, imagesDir, kneeboardsDir);
            }
            if (context.Options.FullExport)
            {
                GenerateFullExport(context.ReportDir, context.MissionTable, dictTable, context.TempDir);
            }
        }

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
                briefingImages = Directory.Exists(imagesDir) ? [.. Directory.GetFiles(imagesDir).Select(Path.GetFileName)] : new List<string>(),
                kneeboards = Directory.Exists(kneeboardsDir) ? Directory.GetFiles(kneeboardsDir, "*.*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(kneeboardsDir, f)).ToList() : []
            };

            string jsonPath = Path.Combine(reportDir, "mission_summary.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"   📄 mission_summary.json created");
        }

        private static void GenerateFullExport(string reportDir, Table mission, Table? dictTable, string tempDir)
        {
            var fullData = new
            {
                mission = MissionUtils.TableToObject(DynValue.NewTable(mission)),
                dictionary = dictTable != null ? MissionUtils.TableToObject(DynValue.NewTable(dictTable)) : null,
                rawFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(tempDir, f)).ToList()
            };

            string fullPath = Path.Combine(reportDir, "mission_full.json");
            File.WriteAllText(fullPath, JsonSerializer.Serialize(fullData, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"   📄 mission_full.json created (complete raw mission data)");
        }
    }
}