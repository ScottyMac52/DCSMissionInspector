using MoonSharp.Interpreter;

namespace DcsMissionReader
{
    public static class MissionUtils
    {
        /// <summary>
        /// Resolves a DynValue that may be a localized string reference (e.g., "DictKey_12345") using the provided dictionary table. If the value is a string that starts with "DictKey_" and the dictionary table is available, it looks up the corresponding string in the dictionary. If the value is not a string or does not start with "DictKey_", it returns the value as a string directly. This method ensures that any localized strings in the mission data are properly resolved for display and reporting purposes.
        /// </summary>
        /// <param name="val">The DynValue to resolve.</param>
        /// <param name="dictTable">The dictionary table used for resolving localized strings.</param>
        /// <returns>The resolved string.</returns>
        public static string Resolve(DynValue val, Table dictTable)
        {
            if (val.Type != DataType.String) return val.ToString() ?? string.Empty;
            string text = val.String;
            if (text.StartsWith("DictKey_") && dictTable != null)
            {
                var resolved = dictTable.Get(text);
                if (resolved.Type == DataType.String) return resolved.String;
            }
            return text;
        }

        /// <summary>
        /// Returns a C# object representation of a MoonSharp DynValue, converting tables to dictionaries and preserving primitive types. This method is used to convert the mission and dictionary tables into a format that can be easily serialized to JSON for the full export. It recursively processes tables, converting them into nested dictionaries, while other data types are returned as their native C# types (e.g., numbers, strings, booleans). This allows for a complete and accurate representation of the mission data in the JSON output.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static object? TableToObject(DynValue val)
        {
            return val.Type switch
            {
                DataType.Table => TableToDictionary(val.Table),
                DataType.Number => val.Number,
                DataType.String => val.String,
                DataType.Boolean => val.Boolean,
                DataType.Nil => null,
                _ => val.ToString()
            };
        }

        /// <summary>
        /// Sanitizes a file name by replacing invalid characters with underscores and trimming whitespace and trailing dots.
        /// </summary>
        /// <param name="name">The file name to sanitize.</param>
        /// <returns>The sanitized file name.</returns>
        public static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "_");
            return name.Trim().TrimEnd('.');
        }

        public static Table? LoadDictionary(string tempDir)
        {
            string dictPath = Path.Combine(tempDir, @"l10n\DEFAULT\dictionary");
            if (!File.Exists(dictPath)) return null;
            var script = new Script();
            script.DoFile(dictPath);
            return script.Globals.Get("dictionary").Table;
        }

        public static string ResolveNameFromGroupId(Table mission, double groupId)
        {
            var coalition = mission.Get("coalition").Table;
            // Iterate through all possible coalitions defined in the mission
            foreach (var side in new[] { "blue", "red", "neutral" })
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countries = sideVal.Table.Get("country").Table;
                foreach (var cPair in countries.Pairs)
                {
                    var country = cPair.Value.Table;
                    // Check all relevant unit categories
                    foreach (var cat in new[] { "plane", "helicopter", "vehicle", "ship", "static" })
                    {
                        var catTable = country.Get(cat)?.Table;
                        if (catTable == null) continue;

                        var groups = catTable.Get("group").Table;
                        foreach (var gPair in groups.Pairs)
                        {
                            var group = gPair.Value.Table;
                            if (group.Get("groupId")?.Number == groupId)
                            {
                                // Found it! Return the name or fallback to type
                                string name = group.Get("name")?.String;
                                if (string.IsNullOrEmpty(name) || name.StartsWith("Group-"))
                                {
                                    return group.Get("units")?.Table?.Get(1)?.Table?.Get("type")?.String ?? "Target";
                                }
                                return name;
                            }
                        }
                    }
                }
            }
            return "Target"; // Final fallback
        }

        public static Table? FindGroupByName(Table mission, string groupName)
        {
            var coalition = mission.Get("coalition").Table;

            // Scan all possible coalitions
            foreach (var side in new[] { "blue", "red", "neutral" })
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countries = sideVal.Table.Get("country").Table;
                foreach (var cPair in countries.Pairs)
                {
                    var country = cPair.Value.Table;

                    // Scan all possible group categories
                    foreach (var cat in new[] { "plane", "helicopter", "vehicle", "ship", "static" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groups = catVal.Table.Get("group").Table;
                        foreach (var gPair in groups.Pairs)
                        {
                            var group = gPair.Value.Table;
                            if (group.Get("name")?.String == groupName)
                            {
                                return group;
                            }
                        }
                    }
                }
            }
            return null;
        }
        public static string FindUnitTypeAtLocation(Table mission, double taskX, double taskY)
        {
            const double tolerance = 50.0; // Meters, adjust based on your mission needs

            var coalition = mission.Get("coalition").Table;
            foreach (var side in new[] { "blue", "red", "neutral" })
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countries = sideVal.Table.Get("country").Table;
                foreach (var cPair in countries.Pairs)
                {
                    var country = cPair.Value.Table;
                    foreach (var cat in new[] { "vehicle", "ship", "static" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groups = catVal.Table.Get("group").Table;
                        foreach (var gPair in groups.Pairs)
                        {
                            var group = gPair.Value.Table;
                            double gx = group.Get("x")?.Number ?? 0;
                            double gy = group.Get("y")?.Number ?? 0;

                            // Calculate distance squared to avoid Math.Sqrt
                            double dx = gx - taskX;
                            double dy = gy - taskY;
                            if ((dx * dx + dy * dy) < (tolerance * tolerance))
                            {
                                // Found a group at this location, return the first unit's type
                                return group.Get("units")?.Table?.Get(1)?.Table?.Get("type")?.String;
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Converts a MoonSharp Table into a C# dictionary, recursively processing nested tables.
        /// </summary>
        /// <param name="table">The MoonSharp Table to convert.</param>
        /// <returns>A dictionary representing the table's key-value pairs.</returns>
        private static Dictionary<string, object?> TableToDictionary(Table table)
        {
            var result = new Dictionary<string, object?>();

            foreach (var pair in table.Pairs)
            {
                string key = DynValueKeyToString(pair.Key);
                result[key] = TableToObject(pair.Value);
            }

            return result;
        }

        /// <summary>
        /// Converts a MoonSharp DynValue key into a string representation, handling different data types.
        /// </summary>
        /// <param name="key">The DynValue key to convert.</param>
        /// <returns>A string representation of the key.</returns>
        private static string DynValueKeyToString(DynValue key)
        {
            return key.Type switch
            {
                DataType.String => key.String,
                DataType.Number => IsWholeNumber(key.Number)
                    ? ((long)key.Number).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : key.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DataType.Boolean => key.Boolean ? "true" : "false",
                DataType.Nil => "null",
                _ => key.ToString() ?? "null"
            };
        }

        /// <summary>
        /// Determines if a double value is a whole number.
        /// </summary>
        /// <param name="value">The double value to check.</param>
        /// <returns>True if the value is a whole number; otherwise, false.</returns>
        private static bool IsWholeNumber(double value)
        {
            return Math.Abs(value % 1) < double.Epsilon;
        }

    }
}
