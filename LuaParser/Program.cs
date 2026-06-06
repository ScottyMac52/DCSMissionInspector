using LuaParser;
using System.Text.Json;

namespace LuaParser
{
    class Program
    {
        static void Main(string[] args)
        {
            string legacyPath = @"C:\Users\vyper\Source\repos\dcs-lua-datamine";
            string modPath = @"D:\SavedGames\DCS.openbeta\Mods\tech\HighDigitSAMs\Database\Vehicle"; // Supply your new path here
            string jsonPath = "threats.json";

            // 1. Run legacy parser (exactly as it was)
            var results = StaticUnitDataParser.ParseDcsFolders(legacyPath);

            // 2. Run new mod processor, merging into results
            StaticUnitDataParser.ProcessAdditionalMods(modPath, results);

            // 3. Export
            string jsonString = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, jsonString);

            Console.WriteLine($"Exported {results.Count} total units. Press any key to exit.");
            Console.ReadKey();
        }
    }
}