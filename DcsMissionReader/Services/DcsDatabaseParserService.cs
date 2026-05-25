using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using System.Collections.Generic;
using System.IO;

namespace DcsMissionReader.Services
{
    public class DcsDatabaseParserService : IDcsDatabaseParserService
    {
        public Dictionary<string, (double trackNm, double fireNm)> LoadRealThreatRanges()
        {
            var ranges = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);

            string dcsPath = FindDcsInstallPath();
            if (!string.IsNullOrEmpty(dcsPath))
            {
                string dbPath = Path.Combine(dcsPath, @"Scripts\Database\db_units.lua");
                if (File.Exists(dbPath))
                    ParseDbFile(dbPath, ranges);
            }

            string savedGamesPath = FindSavedGamesDcsPath();
            if (!string.IsNullOrEmpty(savedGamesPath))
            {
                // Core Saved Games database
                string modDbPath = Path.Combine(savedGamesPath, @"Scripts\Database\db_units.lua");
                if (File.Exists(modDbPath))
                    ParseDbFile(modDbPath, ranges);

                // Any mod folders that might contain their own database
                foreach (var modDir in Directory.GetDirectories(savedGamesPath, "*", SearchOption.TopDirectoryOnly))
                {
                    string modDb = Path.Combine(modDir, @"Scripts\Database\db_units.lua");
                    if (File.Exists(modDb))
                        ParseDbFile(modDb, ranges);
                }
            }

            return ranges;
        }

        private static string FindDcsInstallPath()
        {
            // Registry (most reliable, works on D:, E:, etc.)
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Eagle Dynamics\DCS World");
                if (key?.GetValue("Path") is string path && Directory.Exists(path))
                    return path;

                using var keyOB = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Eagle Dynamics\DCS World OpenBeta");
                if (keyOB?.GetValue("Path") is string pathOB && Directory.Exists(pathOB))
                    return pathOB;
            }
            catch { }

            // Fallback: scan common locations on all fixed drives
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var candidates = new[]
                {
                    Path.Combine(drive.Name, @"Program Files\Eagle Dynamics\DCS World"),
                    Path.Combine(drive.Name, @"Program Files\Eagle Dynamics\DCS World OpenBeta"),
                    Path.Combine(drive.Name, @"DCS World"),
                    Path.Combine(drive.Name, @"DCS World OpenBeta")
                };

                foreach (var candidate in candidates)
                    if (Directory.Exists(candidate))
                        return candidate;
            }

            return null;
        }

        private static string FindSavedGamesDcsPath()
        {
            string savedGames = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Saved Games");

            if (!Directory.Exists(savedGames)) return null;

            var dcsFolders = Directory.GetDirectories(savedGames, "DCS*", SearchOption.TopDirectoryOnly);
            return dcsFolders.FirstOrDefault() ?? Path.Combine(savedGames, "DCS");
        }

        private static void ParseDbFile(string dbPath, Dictionary<string, (double, double)> ranges)
        {
            try
            {
                var script = new Script();
                script.DoFile(dbPath);

                var db = script.Globals.Get("db")?.Table;
                if (db == null) return;

                foreach (var category in new[] { "Ground", "Ship", "Submarine" })
                {
                    var catTable = db.Get(category)?.Table;
                    if (catTable == null) continue;

                    foreach (var pair in catTable.Pairs)
                    {
                        string typeName = pair.Key.ToString().ToLowerInvariant().Replace(" ", "").Replace("-", "");
                        var unit = pair.Value.Table;
                        if (unit == null) continue;

                        double trackM = unit.Get("detection_range")?.Number ?? 3704;   // ~2 NM default
                        double fireM = unit.Get("engagement_range")?.Number ?? 1852;  // ~1 NM default

                        ranges[typeName] = (trackM / 1852.0, fireM / 1852.0);
                    }
                }
            }
            catch { /* silent fallback */ }
        }
    }
}