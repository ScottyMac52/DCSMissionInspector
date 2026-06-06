using DcsMissionReader.Models;
using DcsMissionReader.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LuaParser
{

    public class ThreatDatabaseManager
    {
        public static void UpdateThreats(string jsonFilePath, string luaDirectory)
        {
            // 1. Load existing
            var jsonContent = File.ReadAllText(jsonFilePath);
            var root = JsonNode.Parse(jsonContent).AsObject();

            // 2. Scan and Extract
            var newUnits = ExtractValidUnits(luaDirectory);

            // 3. Append only if non-zero and unique
            foreach (var unit in newUnits)
            {
                if (unit.DetectionRange > 0 || unit.ThreatRange > 0)
                {
                    root[JsonThreatDatabaseService.Canonicalize(unit.Type)] = JsonSerializer.SerializeToNode(unit);
                }
            }

            // 4. Save
            File.WriteAllText(jsonFilePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private static List<ThreatData> ExtractValidUnits(string directory)
        {
            var found = new List<ThreatData>();
            foreach (var file in Directory.GetFiles(directory, "*.lua", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(file);
                // Regex to find the data
                var detMatch = Regex.Match(content, @"DetectionRange\s*[:=]\s*(?<val>\d+)");
                var thrMatch = Regex.Match(content, @"ThreatRange\s*[:=]\s*(?<val>\d+)");

                int det = detMatch.Success ? int.Parse(detMatch.Groups["val"].Value) : 0;
                int thr = thrMatch.Success ? int.Parse(thrMatch.Groups["val"].Value) : 0;

                // Only return units that meet your criteria
                if (det > 0 || thr > 0)
                {
                    found.Add(ExtractThreatData(content));
                }
            }
            return found;
        }

        private static ThreatData ExtractThreatData(string content)
        {
            // Extract the Type (internal ID)
            var nameMatch = Regex.Match(content, @"GT\.Name\s*=\s*[""'](?<type>[^""]+)[""']");
            if (!nameMatch.Success) return null;

            // Extract DisplayName
            var dispMatch = Regex.Match(content, @"GT\.DisplayName\s*=\s*_\((?<disp>.+?)\)");
            string displayName = dispMatch.Success ? dispMatch.Groups["disp"].Value.Trim('\'', '\"') : nameMatch.Groups["type"].Value;

            // Extract Ranges (Detection and Threat)
            // Looking for lines like: GT.sensor.max_range_finding_target = 300000;
            var detMatch = Regex.Match(content, @"max_range_finding_target\s*=\s*(?<val>\d+)");
            var thrMatch = Regex.Match(content, @"distanceMax\s*=\s*(?<val>\d+)"); // Usually inside WS.LN

            return new ThreatData
            {
                Type = nameMatch.Groups["type"].Value,
                DisplayName = displayName,
                DetectionRange = detMatch.Success ? int.Parse(detMatch.Groups["val"].Value) : 0,
                ThreatRange = thrMatch.Success ? int.Parse(thrMatch.Groups["val"].Value) : 0
            };
        }
    }
}