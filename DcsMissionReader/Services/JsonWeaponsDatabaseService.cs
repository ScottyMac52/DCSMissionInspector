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

        public bool IsKnownWeapon(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string cleanValue = NormalizeWeaponKey(value);

            if (_weapons.ContainsKey(cleanValue))
            {
                return true;
            }

            foreach (var pair in _weapons)
            {
                WeaponData weapon = pair.Value;

                if (!string.IsNullOrWhiteSpace(weapon.CLSID)
                    && NormalizeWeaponKey(weapon.CLSID).Equals(cleanValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(weapon.DisplayName)
                    && weapon.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(weapon.DisplayName)
                    && cleanValue.Contains(
                        NormalizeWeaponKey(weapon.DisplayName),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

        private static string NormalizeWeaponKey(string value)
        {
            return value
                .Trim()
                .Trim('{', '}')
                .Replace("\"", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);
        }
    }
}