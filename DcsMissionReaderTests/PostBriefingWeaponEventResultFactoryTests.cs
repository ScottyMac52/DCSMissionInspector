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

        [Fact]
        public void IsWeaponResultEventType_WithNaturalLanguageHitText_ReturnsTrue()
        {
            TacviewEventRecord eventRecord = new()
            {
                EventType = "Message",
                Text = "P_700 has hit Washington",
                Parts = ["Message", "P_700 has hit Washington"],
                TimeSeconds = 120,
                AbsoluteTimeUtc = new DateTime(2016, 6, 21, 4, 32, 0, DateTimeKind.Utc)
            };

            Assert.True(PostBriefingWeaponEventResultFactory.IsWeaponResultEventType(eventRecord));
        }

        [Fact]
        public void IsWeaponResultEventType_WithNaturalLanguageDestroyedText_ReturnsTrue()
        {
            TacviewEventRecord eventRecord = new()
            {
                EventType = "Message",
                Text = "P_700 has destroyed Washington",
                Parts = ["Message", "P_700 has destroyed Washington"],
                TimeSeconds = 180,
                AbsoluteTimeUtc = new DateTime(2016, 6, 21, 4, 33, 0, DateTimeKind.Utc)
            };

            Assert.True(PostBriefingWeaponEventResultFactory.IsWeaponResultEventType(eventRecord));
        }

        [Fact]
        public void CreateWeaponResult_WithNaturalLanguageHitText_NormalizesEventTypeToHit()
        {
            TacviewEventRecord eventRecord = new()
            {
                EventType = "Message",
                Text = "P_700 has hit Washington",
                Parts = ["Message", "P_700 has hit Washington"],
                TimeSeconds = 120,
                AbsoluteTimeUtc = new DateTime(2016, 6, 21, 4, 32, 0, DateTimeKind.Utc)
            };

            IReadOnlyDictionary<string, TacviewObjectTrack> objects =
                new Dictionary<string, TacviewObjectTrack>(StringComparer.OrdinalIgnoreCase)
                {
                    ["1f301"] = CreateTrack(
                        objectId: "1f301",
                        name: "P_700",
                        group: "Kuznetsov Strike Group Escort",
                        pilot: "Pyotr Velikiy",
                        type: "Weapon+Missile",
                        isWeapon: true,
                        timeSeconds: 120),

                    ["301"] = CreateTrack(
                        objectId: "301",
                        name: "CVN_73",
                        group: "Washington CSG",
                        pilot: "Washington",
                        type: "Sea+Watercraft+AircraftCarrier",
                        isWeapon: false,
                        timeSeconds: 120)
                };

            TacviewWeaponResult result =
                PostBriefingWeaponEventResultFactory.CreateWeaponResult(eventRecord, objects);

            Assert.Equal("Hit", result.EventType);
            Assert.Equal("1f301", result.SourceObjectId);
            Assert.Equal("301", result.TargetObjectId);
        }

        [Fact]
        public void CreateWeaponResult_WithNaturalLanguageDestroyedText_NormalizesEventTypeToDestroyed()
        {
            TacviewEventRecord eventRecord = new()
            {
                EventType = "Message",
                Text = "P_700 has destroyed Washington",
                Parts = ["Message", "P_700 has destroyed Washington"],
                TimeSeconds = 180,
                AbsoluteTimeUtc = new DateTime(2016, 6, 21, 4, 33, 0, DateTimeKind.Utc)
            };

            IReadOnlyDictionary<string, TacviewObjectTrack> objects =
                new Dictionary<string, TacviewObjectTrack>(StringComparer.OrdinalIgnoreCase)
                {
                    ["1f401"] = CreateTrack(
                        objectId: "1f401",
                        name: "P_700",
                        group: "Kuznetsov Strike Group Escort",
                        pilot: "Pyotr Velikiy",
                        type: "Weapon+Missile",
                        isWeapon: true,
                        timeSeconds: 180),

                    ["301"] = CreateTrack(
                        objectId: "301",
                        name: "CVN_73",
                        group: "Washington CSG",
                        pilot: "Washington",
                        type: "Sea+Watercraft+AircraftCarrier",
                        isWeapon: false,
                        timeSeconds: 180)
                };

            TacviewWeaponResult result =
                PostBriefingWeaponEventResultFactory.CreateWeaponResult(eventRecord, objects);

            Assert.Equal("Destroyed", result.EventType);
            Assert.Equal("1f401", result.SourceObjectId);
            Assert.Equal("301", result.TargetObjectId);
        }

        private static TacviewObjectTrack CreateTrack(
            string objectId,
            string name,
            string group,
            string pilot,
            string type,
            bool isWeapon,
            double timeSeconds)
        {
            return new TacviewObjectTrack
            {
                ObjectId = objectId,
                Name = name,
                Group = group,
                Pilot = pilot,
                Type = type,
                IsWeapon = isWeapon,
                Samples =
                [
                    new TacviewPositionSample
            {
                TimeSeconds = timeSeconds,
                AbsoluteTimeUtc = new DateTime(2016, 6, 21, 4, 30, 0, DateTimeKind.Utc)
                    .AddSeconds(timeSeconds),
                Longitude = 57.0,
                Latitude = 25.0,
                AltitudeMeters = 0
            }
                ]
            };
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
