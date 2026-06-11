using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class TacviewObjectDisplayNameTests
    {
        [Fact]
        public void GetDisplayName_WithGroupPilotAndObjectId_UsesRequestedIssue27Format()
        {
            TacviewObjectTrack track = new()
            {
                ObjectId = "901",
                Name = "Tu-22M3",
                Pilot = "Aerial-1-1",
                Group = "Red",
                Type = "Air+FixedWing"
            };

            string result = TacviewObjectDisplayName.GetDisplayName(track);

            Assert.Equal("Red-Aerial-1-1", result);
        }

        [Fact]
        public void GetDisplayName_WhenPilotMissing_FallsBackToName()
        {
            TacviewObjectTrack track = new()
            {
                ObjectId = "1701",
                Name = "SH-60B",
                Group = "SAR",
                Type = "Air+Rotorcraft"
            };

            string result = TacviewObjectDisplayName.GetDisplayName(track);

            Assert.Equal("SAR-SH-60B", result);
        }

        [Fact]
        public void GetDisplayName_WhenGroupMissing_UsesIndividualNameAndObjectId()
        {
            TacviewObjectTrack track = new()
            {
                ObjectId = "1501",
                Name = "E-2C",
                Pilot = "Hollywood",
                Type = "Air+FixedWing"
            };

            string result = TacviewObjectDisplayName.GetDisplayName(track);

            Assert.Equal("Hollywood", result);
        }

        [Fact]
        public void GetDisplayName_WhenOnlyObjectIdExists_UsesObjectId()
        {
            TacviewObjectTrack track = new()
            {
                ObjectId = "abc"
            };

            string result = TacviewObjectDisplayName.GetDisplayName(track);

            Assert.Equal("abc", result);
        }
    }
}