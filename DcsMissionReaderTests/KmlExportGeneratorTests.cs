using DcsMissionReader.Models;
using DcsMissionReader.Services.Generators;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using Moq;

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
    }
}