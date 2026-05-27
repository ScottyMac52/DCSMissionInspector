using DcsMissionReader.Models;
using DcsMissionReader.Services.Generators;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using Moq;
using System.Text;

namespace DcsMissionReaderTests
{

    public class KmlExportGeneratorTests
    {
        private readonly Mock<ICoordinateConverterService> _coordMock = new();
        private readonly Mock<IThreatDatabaseService> _threatMock = new();
        private readonly KmlExportGenerator _generator;

        public KmlExportGeneratorTests()
        {
            _generator = new KmlExportGenerator(_coordMock.Object, _threatMock.Object);
        }

        [Fact]
        public void Export_ShouldGenerateValidKmlStructure()
        {
            // Arrange
            var script = new Script();
            var mission = new Table(script);
            // Minimal mission table for KML traversal
            var coalition = new Table(script);
            mission.Set("coalition", DynValue.NewTable(coalition));

            var context = new MissionContext
            {
                ReportDir = ".",
                Sortie = "TestSortie",
                MissionTable = mission
            };

            // Act
            _generator.Export(context);

            // Assert
            string expectedFile = "TestSortie.kml";
            Assert.True(File.Exists(expectedFile));
            string content = File.ReadAllText(expectedFile);
            Assert.Contains("<kml", content);
            Assert.Contains("TestSortie", content);

            File.Delete(expectedFile);
        }

        [Fact]
        public void AppendThreatCircles_WhenThreatExists_AppendsCorrectPolygon()
        {
            // Arrange
            var sb = new StringBuilder();
            var units = new Dictionary<string, int> { { "SA-11", 1 } };
            _threatMock.Setup(s => s.GetThreatRanges("sa11")).Returns((10000.0, 5000.0));

            // Act
            _generator.AppendThreatCircles(sb, 45.0, 40.0, "TestGroup", units);

            // Assert
            var output = sb.ToString();
            Assert.Contains("#yellowCircleStyle", output);
            Assert.Contains("#redCircleStyle", output);
            Assert.Contains("<Polygon>", output);
        }

        [Fact]
        public void ProcessAirGroup_WithValidRoute_AppendsRouteAndWaypointPlacemarks()
        {
            // Arrange
            var kml = new StringBuilder();
            var script = new Script();

            var routeTable = new Table(script);
            var pt1 = new Table(script);
            pt1["x"] = 1000.0;
            pt1["y"] = 2000.0;
            pt1["alt"] = 5000.0;
            pt1["name"] = "Target Alpha";
            routeTable.Append(DynValue.NewTable(pt1));

            var groupData = new MissionGroupData("Viper Flight", "plane", "blue", 0, 0, null, routeTable);

            int blueIdx = 0, redIdx = 0, neutralIdx = 0;
            var blueColors = new[] { "ff00ffff" };

            double expectedLat = 45.12345678;
            double expectedLon = 40.12345678;
            double expectedAlt = 5000.0;

            _coordMock.Setup(c => c.Convert(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()))
                          .Returns((expectedLat, expectedLon));

            // Act
            _generator.ProcessAirGroup(kml, groupData, "Caucasus", blueColors, Array.Empty<string>(), Array.Empty<string>(), ref blueIdx, ref redIdx, ref neutralIdx);

            // Assert
            var output = kml.ToString();

            Assert.Contains("<name>Viper Flight Route</name>", output);
            Assert.Contains("<color>ff00ffff</color>", output);

            // Dynamic string interpolation ensures culture formatting (commas vs decimals) matches exactly
            string expectedCoords = $"{expectedLon:F8},{expectedLat:F8},{expectedAlt:F0}";
            Assert.Contains(expectedCoords, output);

            Assert.Contains("<name>Target Alpha</name>", output);
            Assert.Contains($"Alt: {expectedAlt:F0} m", output);
            Assert.Contains("#wpStyle", output);
        }

        [Fact]
        public void ProcessGroundGroup_WithThreatUnits_CalculatesThreatsAndAppendsPlacemarks()
        {
            // Arrange
            var kml = new StringBuilder();
            var script = new Script();

            // Construct the MoonSharp Table for Units
            var unitsTable = new Table(script);
            var unit1 = new Table(script);
            unit1["type"] = "SA-11 Buk LN 9A310M1";
            var unit2 = new Table(script);
            unit2["type"] = "Ural-375"; // Non-threat unit to test grouping/counting
            unitsTable[1] = unit1;
            unitsTable[2] = unit2;

            var groupData = new MissionGroupData("Red SAM", "vehicle", "red", 5000.0, 5000.0, unitsTable, null);

            // Mock the exact formatted key the service will look up (lowercased, spaces/dashes removed)
            _threatMock.Setup(s => s.GetThreatRanges("sa11bukln9a310m1"))
                              .Returns((20000.0, 10000.0));

            // Act
            _generator.ProcessGroundGroup(kml, groupData, "Caucasus");

            // Assert
            var output = kml.ToString();

            // Base ground unit verification
            Assert.Contains("<name>Red SAM</name>", output);
            Assert.Contains("#redGroundStyle", output);
            Assert.Contains("SA-11 Buk LN 9A310M1 ×1", output);
            Assert.Contains("Ural-375 ×1", output);

            // Threat circle verification
            Assert.Contains("Red SAM - Tracking Range", output);
            Assert.Contains("#yellowCircleStyle", output);

            Assert.Contains("Red SAM - Firing Range", output);
            Assert.Contains("#redCircleStyle", output);
        }

        [Fact]
        public void ProcessAirGroup_WithEmptyRoute_DoesNotAppendAnything()
        {
            // Arrange
            var kml = new StringBuilder();
            var script = new Script();
            var emptyRouteTable = new Table(script); // 0 length

            var groupData = new MissionGroupData("Ghost Flight", "plane", "blue", 0, 0, null, emptyRouteTable);
            int blueIdx = 0, redIdx = 0, neutralIdx = 0;

            // Act
            _generator.ProcessAirGroup(kml, groupData, "Caucasus", new[] { "ff00ffff" }, Array.Empty<string>(), Array.Empty<string>(), ref blueIdx, ref redIdx, ref neutralIdx);

            // Assert
            Assert.Equal(string.Empty, kml.ToString());
        }
    }
}