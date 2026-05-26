using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using System.Text.Json;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DcsMissionReader.Services
{
    public class JsonThreatDatabaseService : IThreatDatabaseService
    {
        private readonly Dictionary<string, ThreatData> _threats;

        public JsonThreatDatabaseService(string jsonFilePath = null)
        {
            string path = jsonFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "threats.json");

            if (!File.Exists(path)) throw new FileNotFoundException(path);

            string json = File.ReadAllText(path);

            // Using default serialization; mapping is handled by [JsonPropertyName] in ThreatData
            _threats = JsonSerializer.Deserialize<Dictionary<string, ThreatData>>(json)
                       ?? [];
        }

        public (double detection, double threat) GetThreatRanges(string missionUnitType)
        {
            if (string.IsNullOrWhiteSpace(missionUnitType)) return (0, 0);

            string lookupKey = Canonicalize(missionUnitType);

            if (_threats.TryGetValue(lookupKey, out var data))
            {
                return (data.DetectionRange, data.ThreatRange);
            }

            return (0, 0);
        }

        /// <summary>
        /// Shared canonicalization logic to match Parser logic.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.", Justification = "<Pending>")]
        public static string Canonicalize(string input)
        {
            return Regex.Replace(input, @"[^a-zA-Z0-9]", "").ToLower();
        }
    }
}