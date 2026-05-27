using MoonSharp.Interpreter;

namespace DcsMissionReader.Services
{
    public class MissionIndexer
    {
        public Dictionary<double, Table> GroupsById { get; } = new();
        public Dictionary<string, Table> GroupsByName { get; } = new();
        public List<(double X, double Y, string UnitType)> UnitLocations { get; } = new();

        public MissionIndexer(Table mission)
        {
            BuildIndex(mission);
        }

        private void BuildIndex(Table mission)
        {
            var coalitionVal = mission.Get("coalition");
            if (coalitionVal.Type != DataType.Table) return;

            foreach (var side in new[] { "blue", "red", "neutral" })
            {
                var sideVal = coalitionVal.Table.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countryListVal = sideVal.Table.Get("country");
                if (countryListVal.Type != DataType.Table) continue;

                foreach (var countryPair in countryListVal.Table.Pairs)
                {
                    if (countryPair.Value.Type != DataType.Table) continue;
                    var country = countryPair.Value.Table;

                    foreach (var cat in new[] { "plane", "helicopter", "ship", "vehicle", "static" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groupListVal = catVal.Table.Get("group");
                        if (groupListVal.Type != DataType.Table) continue;

                        foreach (var gPair in groupListVal.Table.Pairs)
                        {
                            if (gPair.Value.Type != DataType.Table) continue;
                            var group = gPair.Value.Table;

                            // Cache by ID
                            double groupId = group.Get("groupId")?.Number ?? 0;
                            if (groupId > 0) GroupsById[groupId] = group;

                            // Cache by Name
                            string groupName = group.Get("name")?.String;
                            if (!string.IsNullOrEmpty(groupName)) GroupsByName[groupName] = group;

                            // Cache Unit Locations for coordinate lookups
                            var unitsVal = group.Get("units");
                            if (unitsVal.Type == DataType.Table)
                            {
                                foreach (var uPair in unitsVal.Table.Pairs)
                                {
                                    if (uPair.Value.Type != DataType.Table) continue;
                                    var unit = uPair.Value.Table;
                                    double x = unit.Get("x")?.Number ?? 0;
                                    double y = unit.Get("y")?.Number ?? 0;
                                    string type = unit.Get("type")?.String ?? "Unknown";

                                    if (x != 0 || y != 0)
                                        UnitLocations.Add((x, y, type));
                                }
                            }
                        }
                    }
                }
            }
        }

        public string ResolveNameFromGroupId(double groupId)
        {
            if (GroupsById.TryGetValue(groupId, out var group))
                return group.Get("name")?.String ?? "Unknown Target";
            return "Unknown Target";
        }

        public string FindUnitTypeAtLocation(double x, double y, double tolerance = 10.0)
        {
            foreach (var loc in UnitLocations)
            {
                if (Math.Abs(loc.X - x) <= tolerance && Math.Abs(loc.Y - y) <= tolerance)
                    return loc.UnitType;
            }
            return null;
        }
    }
}