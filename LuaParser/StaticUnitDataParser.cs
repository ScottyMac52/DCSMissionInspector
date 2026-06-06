using DcsMissionReader.Models;
using DcsMissionReader.Services;
using System.Text.RegularExpressions;

namespace LuaParser
{
    public class StaticUnitDataParser
    {
        // THIS IS YOUR EXACT ORIGINAL CODE, UNTOUCHED.
        public static Dictionary<string, ThreatData> ParseDcsFolders(string dcsPath)
        {
            var threatDict = new Dictionary<string, ThreatData>();
            string[] luaFiles = Directory.GetFiles(dcsPath, "*.lua", SearchOption.AllDirectories);

            foreach (var file in luaFiles)
            {
                string content = File.ReadAllText(file);
                var typeMatch = Regex.Match(content, @"(?i)type\s*=\s*[""'](?<type>[^""]+)[""']");
                var nameMatch = Regex.Match(content, @"(?i)DisplayName\s*=\s*[""'](?<name>[^""]+)[""']");
                var detMatch = Regex.Match(content, @"(?i)DetectionRange\s*=\s*(?<det>\d+)");
                var engMatch = Regex.Match(content, @"(?i)ThreatRange\s*=\s*(?<eng>\d+)");

                if (typeMatch.Success)
                {
                    string internalId = typeMatch.Groups["type"].Value;
                    string canonicalKey = JsonThreatDatabaseService.Canonicalize(internalId);

                    threatDict[canonicalKey] = new ThreatData
                    {
                        Type = internalId,
                        DisplayName = nameMatch.Success ? nameMatch.Groups["name"].Value : internalId,
                        DetectionRange = detMatch.Success ? int.Parse(detMatch.Groups["det"].Value) : 0,
                        ThreatRange = engMatch.Success ? int.Parse(engMatch.Groups["eng"].Value) : 0
                    };
                }
            }
            return threatDict;
        }

        // NEW: This method processes the GT files specifically.
        public static void ProcessAdditionalMods(string modDirectory, Dictionary<string, ThreatData> existingDict)
        {
            foreach (var file in Directory.GetFiles(modDirectory, "*.lua", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(file);
                if (content.Contains("GT.Name"))
                {
                    var data = ExtractGtThreatData(content);
                    if (data != null && (data.DetectionRange > 0 || data.ThreatRange > 0))
                    {
                        string key = JsonThreatDatabaseService.Canonicalize(data.Type);
                        existingDict[key] = data; // Merges/Updates
                    }
                }
            }
        }

        private static ThreatData ExtractGtThreatData(string content)
        {
            // 1. Get Name and DisplayName (The identifiers)
            var nameMatch = Regex.Match(content, @"GT\.Name\s*=\s*[""'](?<val>[^""]+)[""']");
            var dispMatch = Regex.Match(content, @"GT\.DisplayName\s*=\s*_\((?<val>.+?)\)");
            if (!nameMatch.Success) return null;

            // 2. Extract DetectionRange (Check multiple possible locations)
            // Priority: GT.DetectionRange -> GT.sensor.max_range_finding_target -> GT.WS.maxTargetDetectionRange
            int det = ExtractFirstMatch(content, new[] {
        @"GT\.DetectionRange\s*=\s*(?<val>\d+)",
        @"GT\.sensor\.max_range_finding_target\s*=\s*(?<val>\d+)",
        @"GT\.WS\.maxTargetDetectionRange\s*=\s*(?<val>\d+)"
    });

            // 3. Extract ThreatRange (Check multiple possible locations)
            // Priority: GT.ThreatRange -> GT.WS\[\d+\].LN\[1\].distanceMax
            int thr = ExtractFirstMatch(content, new[] {
        @"GT\.ThreatRange\s*=\s*(?<val>\d+)",
        @"distanceMax\s*=\s*(?<val>\d+)"
    });

            return new ThreatData
            {
                Type = nameMatch.Groups["val"].Value,
                DisplayName = dispMatch.Success ? dispMatch.Groups["val"].Value.Trim('\'', '\"') : nameMatch.Groups["val"].Value,
                DetectionRange = det,
                ThreatRange = thr
            };
        }

        // Helper to try multiple regex patterns until one succeeds
        private static int ExtractFirstMatch(string content, string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var match = Regex.Match(content, pattern);
                if (match.Success) return int.Parse(match.Groups["val"].Value);
            }
            return 0;
        }
    }
}