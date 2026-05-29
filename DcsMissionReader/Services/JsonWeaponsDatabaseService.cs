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

        public string GetWeaponName(string clsid)
        {
            string cleanId = clsid.Trim('{', '}');
            return _weapons.TryGetValue(cleanId, out var w) ? w.DisplayName : clsid;
        }
    }
}