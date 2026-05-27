using DcsMissionReader;
using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using Moq;

namespace DcsMissionReaderTests
{

    public class MissionTargetTests
    {
        private readonly IMissionArchiveService _archiveService;

        public MissionTargetTests()
        {
            _archiveService = new MissionArchiveService();
        }
             

        [Fact(Skip = "Only a locally run test")]
        public void Verify_Tomcat_Target_Resolution_Directly()
        {
            // Arrange: Use the same setup as production
            var missionString = _archiveService.GetMissionContentAsync(@"D:\SavedGames\DCS.openbeta\Missions\F-14B or F-16C Port Stanley BTR-80 times 6 SA19 with TICO and chinese sub.miz").Result;
            var script = new Script();
            script.DoString(missionString);
            Table mission = script.Globals.Get("mission").Table;

            // Act: Manually locate the Tomcat's route points to isolate the task
            // (This follows the same path your HtmlReportGenerator should take)
            var tomcatGroup = MissionUtils.FindGroupByName(mission, "Tomcat"); // You will need to implement a helper for this  
            var points = tomcatGroup?.Get("route")?.Table?.Get("points")?.Table;

            // Assert: Does the task for this point actually contain the data we expect?
            foreach (var pPair in points?.Pairs ?? [])
            {
                var point = pPair.Value.Table;
                var tasks = point.Get("task")?.Table?.Get("params")?.Table?.Get("tasks")?.Table;

                // If this is null here, no amount of HTML code will ever render it.
                Assert.NotNull(tasks);
            }
        }

        private double GetGroupIdByName(Table mission, string name, string side)
        {
            // Helper to find the ID so we can test the resolution logic
            // This traverses the structure using your specific Table API
            var coalitionTable = mission.Get("coalition").Table.Get(side).Table;
            var countries = coalitionTable.Get("country").Table;

            foreach (var cPair in countries.Pairs)
            {
                var groups = cPair.Value.Table.Get("vehicle").Table.Get("group").Table;
                foreach (var gPair in groups.Pairs)
                {
                    var group = gPair.Value.Table;
                    if (group.Get("name").String == name) return group.Get("groupId").Number;
                }
            }
            throw new Exception("Group not found in test mission");
        }
    }
}
