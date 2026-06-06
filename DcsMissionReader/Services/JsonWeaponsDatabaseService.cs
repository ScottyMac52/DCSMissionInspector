using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using System.Text.Json;

namespace DcsMissionReader.Services
{

    public class JsonWeaponDatabaseService : IWeaponDatabaseService
    {
        private readonly Dictionary<string, WeaponData> _weapons;

        public JsonWeaponDatabaseService()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "weapons.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _weapons = JsonSerializer.Deserialize<Dictionary<string, WeaponData>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? [];
            }
            else { _weapons = []; }
        }

        // Add this to JsonWeaponDatabaseService.cs
        // Internal constructor for xUnit testing without touching the file system
        public JsonWeaponDatabaseService(string jsonContent, bool isTest)
        {
            if (!string.IsNullOrWhiteSpace(jsonContent))
            {
                _weapons = JsonSerializer.Deserialize<Dictionary<string, WeaponData>>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new Dictionary<string, WeaponData>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _weapons = new Dictionary<string, WeaponData>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public string GetWeaponName(string clsid)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return "Empty";

            string cleanId = clsid.Trim('{', '}');

            if (_weapons.TryGetValue(cleanId, out var w))
            {
                return w.DisplayName;
            }

            // Fallbacks for missing weapons
            return cleanId.Length == 36
                ? $"Unknown [{cleanId.Substring(0, 8)}]"
                : cleanId.Replace("_", " ");
        }
    }
}