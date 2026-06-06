using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using System.Text;

namespace DcsMissionReader.Services.Generators
{
    public class KmlExportGenerator(ICoordinateConverterService converter, IThreatDatabaseService threatService) : IMissionExportStrategy
    {
        public bool ShouldExport(AppOptions options) => options.CreateKml;

        public void Export(MissionContext context)
        {
            GenerateKmlExport(context.ReportDir, context.Sortie, context.MissionTable, context.Theatre);
        }

        /// <summary>
        /// Generates a KML file for Google Earth that visualizes the flight routes of aircraft groups in the DCS mission. 
        /// The method extracts the route information from the mission table, including the waypoints for each group of planes and helicopters, 
        /// and converts the DCS x/y coordinates to latitude and longitude based on the theatre. 
        /// It then constructs a KML file with placemarks for each group and line strings representing their routes. 
        /// The generated KML file is saved in the specified report directory with a name based on the sortie. 
        /// This allows players to easily view and analyze the flight paths in Google Earth, providing a spatial understanding of the mission's air operations.
        ///
        /// UPDATED: Now also includes all ground targets (vehicles, ships, statics) as colored placemarks using the same coalition colors.
        /// UPDATED: Reads ALL waypoints from every aircraft group.
        /// UPDATED: Uses exact 90° CCW conversion calibrated to your points.
        /// </summary>
        /// <param name="reportDir">The directory where the KML file will be saved.</param>
        /// <param name="sortie">The name of the sortie.</param>
        /// <param name="mission">The mission table containing the mission data.</param>
        /// <param name="theatre">The theatre of operations.</param>
        private void GenerateKmlExport(string reportDir, string sortie, Table mission, string theatre)
        {
            string kmlPath = Path.Combine(reportDir, MissionUtils.SanitizeFileName(sortie) + ".kml");
            var kml = new System.Text.StringBuilder();
            kml.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            kml.AppendLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2"">");
            kml.AppendLine("<Document>");
            kml.AppendLine($"<name>{EscapeForKml(sortie + " - DCS Mission Routes & Targets")}</name>");

            // Coalition-specific color palettes (ABGR format) - expanded for many routes
            var blueColors = new[] {
                "ff00ffff", "ff00b3ff", "ff0080ff", "ff0066ff", "ff0040ff",
                "ff00ccff", "ff00aaff", "ff0099ff", "ff0077ff", "ff0055ff",
                "ff3399ff", "ff66aaff", "ff99ccff"
            };
            var redColors = new[] {
                "ff0000ff", "ff0066ff", "ff00a5ff", "ffff00ff", "ffff3399",
                "ffff0066", "ffff3366", "ffff6633", "ffff9933", "ffffcc33",
                "ffff6600", "ffff9900", "ffffcc00"
            };
            var neutralColors = new[] {
                "ff00ff80", "ff80ff00", "ffccff00", "ffffff00", "ffccff66",
                "ff99ff66", "ff66ff99", "ff33ffcc", "ff00ffcc", "ff66ffcc",
                "ff99ffcc", "ffccffcc", "ffffffcc"
            };

            int blueIdx = 0, redIdx = 0, neutralIdx = 0;

            // Waypoint placemark style
            kml.AppendLine(@"<Style id=""wpStyle""><IconStyle><scale>1.2</scale><Icon><href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            // Ground target style
            kml.AppendLine(@"<Style id=""groundStyle""><IconStyle><scale>1.0</scale><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            // Coalition-colored ground unit markers
            kml.AppendLine(@"<Style id=""blueGroundStyle""><IconStyle><scale>1.0</scale><color>ff00ffff</color><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            kml.AppendLine(@"<Style id=""redGroundStyle""><IconStyle><scale>1.0</scale><color>ff0000ff</color><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            kml.AppendLine(@"<Style id=""neutralGroundStyle""><IconStyle><scale>1.0</scale><color>ffffffff</color><Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon></IconStyle><LabelStyle><scale>1.0</scale></LabelStyle></Style>");
            // Threat circle styles
            kml.AppendLine(@"<Style id=""yellowCircleStyle""><LineStyle><color>ff00ffff</color><width>3</width></LineStyle><PolyStyle><color>2000ffff</color></PolyStyle></Style>");
            kml.AppendLine(@"<Style id=""redCircleStyle""><LineStyle><color>ff0000ff</color><width>3</width></LineStyle><PolyStyle><color>200000ff</color></PolyStyle></Style>");

            // === PROCESS ALL GROUPS ===
            var allGroups = GetAllGroups(mission);
            foreach (var group in allGroups)
            {
                if (group.Category == "plane" || group.Category == "helicopter")
                {
                    ProcessAirGroup(kml, group, theatre, blueColors, redColors, neutralColors, ref blueIdx, ref redIdx, ref neutralIdx);
                }
                else if (group.Category == "vehicle" || group.Category == "ship" || group.Category == "static")
                {
                    ProcessGroundGroup(kml, group, theatre);
                }
            }

            kml.AppendLine("</Document>");
            kml.AppendLine("</kml>");

            File.WriteAllText(kmlPath, kml.ToString());
            Console.WriteLine($" 🗺️ KML created with FULL routes + ALL waypoints + ground targets → {kmlPath}");
        }

        public void ProcessAirGroup(StringBuilder kml, MissionGroupData group, string theatre, string[] blueColors, string[] redColors, string[] neutralColors, ref int blueIdx, ref int redIdx, ref int neutralIdx)
        {
            if (group.RoutePoints == null || group.RoutePoints.Length == 0) return;

            string color = group.Side == "blue" ? blueColors[blueIdx++ % blueColors.Length] :
                           group.Side == "red" ? redColors[redIdx++ % redColors.Length] :
                           neutralColors[neutralIdx++ % neutralColors.Length];

            // Full route LineString
            kml.AppendLine("<Placemark>");
            kml.AppendLine($"<name>{EscapeForKml(group.Name)} Route</name>");
            kml.AppendLine($@"<Style><LineStyle><color>{color}</color><width>5</width></LineStyle></Style>");
            kml.AppendLine("<LineString>");
            kml.AppendLine("<tessellate>1</tessellate>");
            kml.AppendLine("<coordinates>");
            GenerateWaypointPlacemarks(theatre, kml, group.RoutePoints);
            kml.AppendLine("</coordinates>");
            kml.AppendLine("</LineString>");
            kml.AppendLine("</Placemark>");

            // Individual waypoint placemarks (ALL of them)
            for (int i = 1; i <= group.RoutePoints.Length; i++)
            {
                var pt = group.RoutePoints.Get(i);
                if (pt.Type != DataType.Table) continue;
                double x = pt.Table.Get("x")?.Number ?? 0;
                double y = pt.Table.Get("y")?.Number ?? 0;
                double alt = pt.Table.Get("alt")?.Number ?? 0;
                string wpName = $"{group.Name} WP{i}";

                // Apply Origin Calibration
                var (lat, lon) = GetLatLonFromDcs(x, y, theatre);
                kml.AppendLine("<Placemark>");
                kml.AppendLine($"<name>{EscapeForKml(wpName)}</name>");
                kml.AppendLine($"<description><![CDATA[Waypoint {i}<br/>Alt: {alt:F0} m]]></description>");
                kml.AppendLine("<styleUrl>#wpStyle</styleUrl>");
                kml.AppendLine("<Point>");
                kml.AppendLine($"<coordinates>{lon:F8},{lat:F8},{alt:F0}</coordinates>");
                kml.AppendLine("</Point>");
                kml.AppendLine("</Placemark>");
            }
        }

        public void ProcessGroundGroup(StringBuilder kml, MissionGroupData group, string theatre)
        {
            var unitCounts = new Dictionary<string, int>();
            if (group.Units != null)
            {
                for (int u = 1; u <= group.Units.Length; u++)
                {
                    var unit = group.Units.Get(u);
                    if (unit.Type != DataType.Table) continue;
                    string uType = unit.Table.Get("type")?.String ?? "Unknown";
                    unitCounts[uType] = unitCounts.GetValueOrDefault(uType, 0) + 1;
                }
            }

            string unitInfo = unitCounts.Count > 0
                ? string.Join(", ", unitCounts.Select(kv => $"{kv.Key} ×{kv.Value}"))
                : group.Category.ToUpper();

            var (lat, lon) = GetLatLonFromDcs(group.X, group.Y, theatre);
            string markerStyle = group.Side == "blue" ? "#blueGroundStyle" :
                                 group.Side == "red" ? "#redGroundStyle" :
                                 "#neutralGroundStyle";

            kml.AppendLine("<Placemark>");
            kml.AppendLine($"<name>{EscapeForKml(group.Name)}</name>");
            kml.AppendLine($"<description><![CDATA[{group.Category.ToUpper()} • {unitInfo}<br/>{group.X:F0}, {group.Y:F0}]]></description>");
            kml.AppendLine($"<styleUrl>{markerStyle}</styleUrl>");
            kml.AppendLine("<Point>");
            kml.AppendLine($"<coordinates>{lon:F8},{lat:F8},0</coordinates>");
            kml.AppendLine("</Point>");
            kml.AppendLine("</Placemark>");

            // Threat circles
            AppendThreatCircles(kml, lat, lon, group.Name, unitCounts);
        }

        public void AppendThreatCircles(StringBuilder kml, double lat, double lon, string groupName, Dictionary<string, int> unitCounts)
        {
            bool hasThreat = false;
            double maxTrack = 0, maxFire = 0;
            foreach (var type in unitCounts.Keys)
            {
                string key = type.ToLower().Replace(" ", "").Replace("-", "");
                if (threatService.GetThreatRanges(key) is (double track, double fire))
                {
                    hasThreat = true;
                    maxTrack = Math.Max(maxTrack, track);
                    maxFire = Math.Max(maxFire, fire);
                }
            }

            if (hasThreat && maxTrack > 0)
            {
                double maxTrackNm = maxTrack / 1852.0;
                double maxFireNm = maxFire / 1852.0;

                string yellowCoords = GenerateCircle(lat, lon, maxTrackNm);
                kml.AppendLine("<Placemark>");
                kml.AppendLine($"<name>{EscapeForKml(groupName)} - Tracking Range</name>");
                kml.AppendLine("<styleUrl>#yellowCircleStyle</styleUrl>");
                kml.AppendLine("<Polygon><outerBoundaryIs><LinearRing><coordinates>" + yellowCoords + "</coordinates></LinearRing></outerBoundaryIs></Polygon>");
                kml.AppendLine("</Placemark>");

                if (maxFire > 0 && maxFire < maxTrack)
                {
                    string redCoords = GenerateCircle(lat, lon, maxFireNm);
                    kml.AppendLine("<Placemark>");
                    kml.AppendLine($"<name>{EscapeForKml(groupName)} - Firing Range</name>");
                    kml.AppendLine("<styleUrl>#redCircleStyle</styleUrl>");
                    kml.AppendLine("<Polygon><outerBoundaryIs><LinearRing><coordinates>" + redCoords + "</coordinates></LinearRing></outerBoundaryIs></Polygon>");
                    kml.AppendLine("</Placemark>");
                }
            }
        }

        private void GenerateWaypointPlacemarks(string theatre, System.Text.StringBuilder kml, Table pointsVal)
        {
            for (int i = 1; i <= pointsVal.Length; i++)
            {
                var pt = pointsVal.Get(i);
                if (pt.Type != DataType.Table) continue;
                double x = pt.Table.Get("x")?.Number ?? 0;
                double y = pt.Table.Get("y")?.Number ?? 0;
                double alt = pt.Table.Get("alt")?.Number ?? 0;

                // Apply Origin Calibration
                var (lat, lon) = GetLatLonFromDcs(x, y, theatre);
                kml.AppendLine($" {lon:F8},{lat:F8},{alt:F0}");
            }
        }

        private static string GenerateCircle(double centerLat, double centerLon, double radiusNm, int segments = 36)
        {
            var sb = new System.Text.StringBuilder();
            double radiusRad = radiusNm * 1852.0 / 6371000.0;
            for (int i = 0; i <= segments; i++)
            {
                double angle = i * 2 * Math.PI / segments;
                double dLat = radiusRad * Math.Cos(angle);
                double dLon = radiusRad * Math.Sin(angle) / Math.Cos(centerLat * Math.PI / 180.0);
                double lat = centerLat + dLat * (180.0 / Math.PI);
                double lon = centerLon + dLon * (180.0 / Math.PI);
                sb.Append($"{lon:F8},{lat:F8},0 ");
            }
            return sb.ToString().Trim();
        }

        private (double lat, double lon) GetLatLonFromDcs(double x, double y, string theatre)
        {
            return converter.Convert(x, y, theatre);
        }

        static string EscapeForKml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        public static List<MissionGroupData> GetAllGroups(Table mission)
        {
            var groups = new List<MissionGroupData>();
            var coalition = mission.Get("coalition").Table;

            foreach (var side in new[] { "blue", "red", "neutrals" }) // Fixed "neutral" to "neutrals" to match DCS table structure
            {
                var sideVal = coalition.Get(side);
                if (sideVal.Type != DataType.Table) continue;

                var countries = sideVal.Table.Get("country").Table;
                foreach (var c in countries.Pairs)
                {
                    var country = c.Value.Table;
                    foreach (var cat in new[] { "plane", "helicopter", "vehicle", "ship", "static" })
                    {
                        var catVal = country.Get(cat);
                        if (catVal.Type != DataType.Table) continue;

                        var groupList = catVal.Table.Get("group").Table;
                        foreach (var g in groupList.Pairs)
                        {
                            var group = g.Value.Table;

                            // Safely extract route points if they exist (for planes/helos)
                            Table routePoints = null;
                            var routeVal = group.Get("route");
                            if (routeVal.Type == DataType.Table)
                            {
                                var pointsVal = routeVal.Table.Get("points");
                                if (pointsVal.Type == DataType.Table && pointsVal.Table.Length > 0)
                                {
                                    routePoints = pointsVal.Table;
                                }
                            }

                            groups.Add(new MissionGroupData(
                                group.Get("name")?.String ?? "Unknown",
                                cat,
                                side,
                                group.Get("x")?.Number ?? 0,
                                group.Get("y")?.Number ?? 0,
                                group.Get("units").Table,
                                routePoints // Pass the extracted points
                            ));
                        }
                    }
                }
            }
            return groups;
        }
    }
}
