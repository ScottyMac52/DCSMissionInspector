using DcsMissionReader.Models;
using DcsMissionReader.Services.Generators;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using Moq;

namespace DcsMissionReaderTests
{
    public class HtmlReportGeneratorTests
    {
        private readonly Mock<IFileManagementService> _fileMock = new();
        private readonly HtmlReportGenerator _generator;

        public HtmlReportGeneratorTests()
        {
            _generator = new HtmlReportGenerator(_fileMock.Object);
        }

        [Fact]
        public void Export_ShouldCreateDirectoriesAndCopyFiles()
        {
            // Arrange
            var script = new Script();
            var missionTable = new Table(script);

            // Add required metadata keys that cause the NullReference
            missionTable.Set("date", DynValue.NewTable(new Table(script)));
            missionTable.Get("date").Table.Set("Year", DynValue.NewNumber(2026));
            missionTable.Get("date").Table.Set("Month", DynValue.NewNumber(5));
            missionTable.Get("date").Table.Set("Day", DynValue.NewNumber(26));
            missionTable.Set("start_time", DynValue.NewNumber(36000));
            missionTable.Set("version", DynValue.NewString("1.0"));

            var context = new MissionContext
            {
                ReportDir = "test",
                TempDir = "temp",
                Sortie = "TestSortie",
                MissionTable = missionTable,
                Options = new AppOptions() { CreateHtml = true }
            };

            // Act
            _generator.Export(context);

            // Assert
            _fileMock.Verify(m => m.CopyImages("temp", It.IsAny<string>()), Times.Once);
            Assert.True(Directory.Exists(Path.Combine("test", "images")));

            // Cleanup
            if (Directory.Exists("test")) Directory.Delete("test", true);
        }

        [Fact]
        public void ParseWaypoints_ShouldExtractCorrectData_FromTable()
        {
            // Arrange: Create a minimal Lua table structure
            var script = new Script();
            var mission = new Table(script);
            var points = new Table(script);
            var wp1 = new Table(script);
            wp1.Set("x", DynValue.NewNumber(100));
            wp1.Set("y", DynValue.NewNumber(200));
            points.Set(1, DynValue.NewTable(wp1));

            // Act
            var results = HtmlReportGenerator.ParseWaypoints(points, mission);

            // Assert
            Assert.Single(results);
            Assert.Equal(100, results[0].x);
            Assert.Equal(200, results[0].y);
        }
    }
}
