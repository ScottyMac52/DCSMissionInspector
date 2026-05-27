using DcsMissionReader.Models;
using DcsMissionReader.Services.Generators;
using MoonSharp.Interpreter;
using System.Text.Json;

namespace DcsMissionReaderTests
{

    public class JsonSummaryGeneratorTests
    {
        private readonly JsonSummaryGenerator _generator = new();

        [Fact]
        public void Export_ShouldGenerateJsonSummaryFile()
        {
            // Arrange
            var script = new Script();
            var missionTable = new Table(script);

            // Populate required metadata
            missionTable.Set("date", DynValue.NewTable(new Table(script)));
            missionTable.Get("date").Table.Set("Year", DynValue.NewNumber(2026));
            missionTable.Get("date").Table.Set("Month", DynValue.NewNumber(5));
            missionTable.Get("date").Table.Set("Day", DynValue.NewNumber(26));
            missionTable.Set("start_time", DynValue.NewNumber(36000));
            missionTable.Set("version", DynValue.NewString("1.0"));

            var context = new MissionContext
            {
                ReportDir = "test_json",
                TempDir = "temp_json",
                Sortie = "JsonSortie",
                MissionTable = missionTable,
                Options = new AppOptions { CreateJson = true }
            };

            Directory.CreateDirectory("test_json");

            // Act
            _generator.Export(context);

            // Assert
            string jsonPath = Path.Combine("test_json", "mission_summary.json");
            Assert.True(File.Exists(jsonPath));

            var jsonContent = File.ReadAllText(jsonPath);
            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                Assert.Equal("JsonSortie", doc.RootElement.GetProperty("name").GetString());
            }

            // Cleanup
            Directory.Delete("test_json", true);
        }
    }
}