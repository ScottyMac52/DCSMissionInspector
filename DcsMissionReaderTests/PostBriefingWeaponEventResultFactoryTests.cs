using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class PostBriefingWeaponEventResultFactoryTests
    {
        private static readonly DateTime ReferenceTimeUtc = new(2016, 6, 21, 4, 30, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData("Destroyed", true)]
        [InlineData("Timeout", true)]
        [InlineData("Hit", true)]
        [InlineData("Damage", true)]
        [InlineData("Damaged", true)]
        [InlineData("Message", false)]
        [InlineData("LeftArea", false)]
        public void IsWeaponResultEventType_ReturnsExpectedResult(string eventType, bool expectedResult)
        {
            TacviewEventRecord eventRecord = CreateEventRecord(
                eventType,
                text: eventType,
                parts: [eventType]);

            bool result = PostBriefingWeaponEventResultFactory.IsWeaponResultEventType(eventRecord);

            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void CreateWeaponResult_WithExplicitSourceAndTargetIds_UsesExplicitIds()
        {
            Dictionary<string, TacviewObjectTrack> objects = CreateObjects(
                CreateObjectTrack("301", "CVN_73", "Sea+Watercraft+AircraftCarrier", "Washington CSG", "Washington"),
                CreateObjectTrack("1f401", "P_700", "Weapon+Missile", "Kuznetsov Strike Group Escort", "P_700"));

            TacviewEventRecord eventRecord = CreateEventRecord(
                "Destroyed",
                "P_700 has destroyed Washington CSG-Washington",
                parts:
                [
                    "Destroyed",
                    "SourceId:1f401",
                    "TargetId:301",
                    "P_700 has destroyed Washington CSG-Washington"
                ]);

            TacviewWeaponResult result = PostBriefingWeaponEventResultFactory.CreateWeaponResult(
                eventRecord,
                objects);

            Assert.Equal("Destroyed", result.EventType);
            Assert.Equal("1f401", result.SourceObjectId);
            Assert.Equal("301", result.TargetObjectId);
            Assert.Equal("P_700", result.SourceName);
            Assert.Equal("CVN_73", result.TargetName);
            Assert.Equal(eventRecord.Text, result.Description);
        }

        [Fact]
        public void CreateWeaponResult_WithNaturalLanguageHit_ResolvesWeaponSourceAndTargetWhenIdsAreMissing()
        {
            Dictionary<string, TacviewObjectTrack> objects = CreateObjects(
                CreateObjectTrack("301", "CVN_73", "Sea+Watercraft+AircraftCarrier", "Washington CSG", "Washington"),
                CreateObjectTrack("1f3fe", "P_700", "Weapon+Missile", "Kuznetsov Strike Group Escort", "P_700"));

            TacviewEventRecord eventRecord = CreateEventRecord(
                "Hit",
                "P_700 has hit Washington CSG-Washington",
                parts:
                [
                    "Hit",
                    "P_700 has hit Washington CSG-Washington"
                ]);

            TacviewWeaponResult result = PostBriefingWeaponEventResultFactory.CreateWeaponResult(
                eventRecord,
                objects);

            Assert.Equal("Hit", result.EventType);
            Assert.Equal("1f3fe", result.SourceObjectId);
            Assert.Equal("301", result.TargetObjectId);
            Assert.Equal("P_700", result.SourceName);
            Assert.Equal("CVN_73", result.TargetName);
            Assert.Equal(eventRecord.Text, result.Description);
        }

        [Fact]
        public void CreateWeaponResult_WithNaturalLanguageDestroyed_ResolvesWeaponSourceAndTargetWhenIdsAreMissing()
        {
            Dictionary<string, TacviewObjectTrack> objects = CreateObjects(
                CreateObjectTrack("301", "CVN_73", "Sea+Watercraft+AircraftCarrier", "Washington CSG", "Washington"),
                CreateObjectTrack("1f401", "P_700", "Weapon+Missile", "Kuznetsov Strike Group Escort", "P_700"));

            TacviewEventRecord eventRecord = CreateEventRecord(
                "Destroyed",
                "P_700 has destroyed Washington CSG-Washington",
                parts:
                [
                    "Destroyed",
                    "P_700 has destroyed Washington CSG-Washington"
                ]);

            TacviewWeaponResult result = PostBriefingWeaponEventResultFactory.CreateWeaponResult(
                eventRecord,
                objects);

            Assert.Equal("Destroyed", result.EventType);
            Assert.Equal("1f401", result.SourceObjectId);
            Assert.Equal("301", result.TargetObjectId);
            Assert.Equal("P_700", result.SourceName);
            Assert.Equal("CVN_73", result.TargetName);
        }

        [Fact]
        public void CreateWeaponResult_WithNaturalLanguageDamaged_ResolvesWeaponSourceAndTargetWhenIdsAreMissing()
        {
            Dictionary<string, TacviewObjectTrack> objects = CreateObjects(
                CreateObjectTrack("301", "CVN_73", "Sea+Watercraft+AircraftCarrier", "Washington CSG", "Washington"),
                CreateObjectTrack("1f3ff", "P_700", "Weapon+Missile", "Kuznetsov Strike Group Escort", "P_700"));

            TacviewEventRecord eventRecord = CreateEventRecord(
                "Damaged",
                "P_700 has damaged Washington CSG-Washington",
                parts:
                [
                    "Damaged",
                    "P_700 has damaged Washington CSG-Washington"
                ]);

            TacviewWeaponResult result = PostBriefingWeaponEventResultFactory.CreateWeaponResult(
                eventRecord,
                objects);

            Assert.Equal("Damaged", result.EventType);
            Assert.Equal("1f3ff", result.SourceObjectId);
            Assert.Equal("301", result.TargetObjectId);
            Assert.Equal("P_700", result.SourceName);
            Assert.Equal("CVN_73", result.TargetName);
        }

        [Fact]
        public void CreateWeaponResult_WithNaturalLanguageHit_DoesNotResolveNonWeaponAsSource()
        {
            Dictionary<string, TacviewObjectTrack> objects = CreateObjects(
                CreateObjectTrack("301", "CVN_73", "Sea+Watercraft+AircraftCarrier", "Washington CSG", "Washington"),
                CreateObjectTrack("401", "P_700", "Sea+Watercraft+Cruiser", "Kuznetsov Strike Group Escort", "Pyotr Velikiy"));

            TacviewEventRecord eventRecord = CreateEventRecord(
                "Hit",
                "P_700 has hit Washington CSG-Washington",
                parts:
                [
                    "Hit",
                    "P_700 has hit Washington CSG-Washington"
                ]);

            TacviewWeaponResult result = PostBriefingWeaponEventResultFactory.CreateWeaponResult(
                eventRecord,
                objects);

            Assert.Null(result.SourceObjectId);
            Assert.Equal("301", result.TargetObjectId);
            Assert.Equal("CVN_73", result.TargetName);
        }

        private static TacviewEventRecord CreateEventRecord(
            string eventType,
            string text,
            IReadOnlyList<string> parts,
            double timeSeconds = 20)
        {
            return new TacviewEventRecord
            {
                EventType = eventType,
                TimeSeconds = timeSeconds,
                AbsoluteTimeUtc = ReferenceTimeUtc.AddSeconds(timeSeconds),
                Text = text,
                Parts = parts
            };
        }

        private static TacviewObjectTrack CreateObjectTrack(
            string objectId,
            string name,
            string type,
            string group,
            string pilot)
        {
            return new TacviewObjectTrack
            {
                ObjectId = objectId,
                Name = name,
                Type = type,
                Group = group,
                Pilot = pilot
            };
        }

        private static Dictionary<string, TacviewObjectTrack> CreateObjects(
            params TacviewObjectTrack[] tracks)
        {
            return tracks.ToDictionary(
                track => track.ObjectId,
                track => track,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
