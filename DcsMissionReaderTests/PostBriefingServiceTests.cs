using DcsMissionReader.Models;
using DcsMissionReader.Services;
using DcsMissionReader.Services.Interfaces;
using Moq;
using System.IO.Compression;

namespace DcsMissionReaderTests
{
    public sealed class PostBriefingServiceTests
    {
        [Fact]
        public void CreatePostBriefingKml_WithValidZippedAcmi_CreatesKmlWithTracksKnownWeaponsAndResults()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "sample.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "sample.postbrief.kml");

                CreateAcmiZip(zipPath, BuildSampleAcmi());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: ["AIM-120C"]);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(zipPath, result.SourceAcmiZipFilePath);
                Assert.Equal(outputPath, result.OutputKmlFilePath);
                Assert.Equal(1, result.GroupTrackCount);
                Assert.Equal(1, result.WeaponEmploymentCount);
                Assert.Equal(1, result.WeaponResultCount);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("<kml", kml);
                Assert.Contains("Object Tracks", kml);
                Assert.Contains("Weapon Employment", kml);
                Assert.Contains("Weapon Results and Events", kml);

                Assert.Contains("Springfield 1", kml);
                Assert.DoesNotContain("Springfield 1 START", kml);
                Assert.DoesNotContain("Springfield 1 END", kml);

                Assert.Contains("AIM-120C", kml);
                Assert.Contains("Destroyed", kml);
                Assert.Contains("48.66236111,29.96027500,1500.00", kml);

                weaponDatabaseMock.Verify(
                    x => x.IsKnownWeapon(It.IsAny<string>()),
                    Times.AtLeastOnce);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithUnknownWeaponLikeObject_DoesNotCreateWeaponEmployment()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "fuel-tank-test.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "fuel-tank-test.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithFuelTankAndKnownWeapon());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: ["AIM-120C"]);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));

                // Only AIM-120C should be treated as weapon employment.
                // Fuel tanks should not become weapon employments.
                Assert.Equal(1, result.WeaponEmploymentCount);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("AIM-120C", kml);
                Assert.DoesNotContain("Fuel Tank", kml, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Drop Tank", kml, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithMaxTrackPoints_LimitsPlottedTrackPointsAcrossFullTrack()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "many-points.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "many-points.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithManyTrackPoints(pointCount: 20));

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                var options = new DcsMissionReader.Models.PostBriefingKmlOptions
                {
                    MaxTrackPointsPerObject = 5
                };

                service.CreatePostBriefingKml(zipPath, outputPath, options);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("Point 1", kml);
                Assert.Contains("Point 5", kml);
                Assert.DoesNotContain("Point 6", kml);

                // Proves sampling spans the whole track, not just the first 5 samples.
                Assert.Contains("48.00000000,29.00000000,1000.00", kml);
                Assert.Contains("48.01900000,29.01900000,1019.00", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithoutExplicitOutputPath_CreatesDefaultPostBriefKml()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "mission.acmi.zip");

                CreateAcmiZip(zipPath, BuildSampleAcmi());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: ["AIM-120C"]);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                var result = service.CreatePostBriefingKml(zipPath);

                Assert.True(File.Exists(result.OutputKmlFilePath));
                Assert.EndsWith("mission.postbrief.kml", result.OutputKmlFilePath);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithMissingFile_ThrowsFileNotFoundException()
        {
            Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                knownWeapons: []);

            var service = new PostBriefingService(weaponDatabaseMock.Object);

            Assert.Throws<FileNotFoundException>(() =>
                service.CreatePostBriefingKml(@"C:\does-not-exist\missing.acmi.zip"));
        }

        [Fact]
        public void CreatePostBriefingKml_WithEmptyPath_ThrowsArgumentException()
        {
            Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                knownWeapons: []);

            var service = new PostBriefingService(weaponDatabaseMock.Object);

            Assert.Throws<ArgumentException>(() =>
                service.CreatePostBriefingKml(""));
        }

        [Fact]
        public void CreatePostBriefingKml_WithEmptyZip_ThrowsInvalidDataException()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "empty.zip");

                using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                }

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                Assert.Throws<InvalidDataException>(() =>
                    service.CreatePostBriefingKml(zipPath));
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Theory]
        [InlineData("--post-brief")]
        [InlineData("--post_brief")]
        [InlineData("--postbrief")]
        public void SampleAcmiZip_WithSupportedPostBriefSwitchNames_IsDocumentedScenario(string switchName)
        {
            Assert.StartsWith("--post", switchName, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CreatePostBriefingKml_WithChaffAndFlare_DoesNotCreateTracksOrWeaponEmployments()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "countermeasures.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "countermeasures.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithCountermeasures());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: ["AIM-120C"]);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));

                // Only the aircraft should remain as a group track.
                // Chaff and flares should not become tracks.
                Assert.Equal(1, result.GroupTrackCount);

                // No known weapons were fired in this ACMI sample.
                Assert.Equal(0, result.WeaponEmploymentCount);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("Springfield 1", kml);
                Assert.DoesNotContain("Chaff", kml, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Flare", kml, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Decoy", kml, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Misc+Decoy+Chaff", kml, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithTacviewAllies_DoesNotAutomaticallyColorEverythingBlue()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "allies-relative.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "allies-relative.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithTacviewRelativeAllies());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                var options = new PostBriefingKmlOptions
                {
                    TreatTacviewAlliesAsBlue = false,
                    TreatTacviewEnemiesAsRed = false,
                    InferBlueForKnownUsNavalAssets = false
                };

                service.CreatePostBriefingKml(zipPath, outputPath, options);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("Tacview Coalition: Allies", kml);
                Assert.Contains("Tactical Side: Neutral/Unknown", kml);
                Assert.Contains("#neutralTrackStyle", kml);
                Assert.DoesNotContain("#blueTrackStyle", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithBullseyeObject_RendersBullseyeInsteadOfTrack()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "bullseye.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "bullseye.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithBullseye());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                service.CreatePostBriefingKml(zipPath, outputPath, new PostBriefingKmlOptions
                {
                    TreatTacviewAlliesAsBlue = false,
                    TreatTacviewEnemiesAsRed = false,
                    InferBlueForKnownUsNavalAssets = true
                });

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("Blue Bullseye", kml);
                Assert.Contains("#blueBullseyeStyle", kml);
                Assert.Contains("Blue Bullseye 10 NM Ring", kml);
                Assert.Contains("Blue Bullseye 25 NM Ring", kml);
                Assert.Contains("Blue Bullseye 50 NM Ring", kml);

                Assert.DoesNotContain("Blue Bullseye TRACK", kml, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithMissionMetadata_CreatesMissionFolder()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "mission-info.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "mission-info.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithMissionMetadata());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(outputPath, result.OutputKmlFilePath);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("<name>Mission</name>", kml);
                Assert.Contains("UH-1_MAR_IA_Weapons Range", kml);
                Assert.Contains("Category: CAS", kml);
                Assert.Contains("Author: Spaz", kml);
                Assert.Contains("Data Source: DCS 2.9.5.55918", kml);
                Assert.Contains("Data Recorder: DCS2ACMI 1.9.3.200", kml);
                Assert.Contains("File Type: text/acmi/tacview", kml);
                Assert.Contains("File Version: 2.1", kml);
                Assert.Contains("Reference Time: 2016-03-21T04:30:00.0000000Z", kml);
                Assert.Contains("Recording Time: 2024-07-06T17:56:08.1450000Z", kml);
                Assert.Contains("Reference Latitude: 10", kml);
                Assert.Contains("Reference Longitude: 141", kml);

                // Proves escaped comma was preserved instead of splitting Comments into bad tokens.
                Assert.Contains(
                    "The weather is overcast with rain, but the range is open.",
                    kml);

                // Proves multi-line briefing was stitched back together.
                Assert.Contains("You are FORD 2 1, a single USMC UH-1", kml);
                Assert.Contains("Communications:", kml);
                Assert.Contains("Range Control and LHA-1 Tower: 251.00 MHz", kml);

                // Mission reference point should be plotted from ReferenceLongitude/ReferenceLatitude.
                Assert.Contains("141.00000000,10.00000000,0", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithEscapedCommasInMissionMetadata_DoesNotSplitMetadataValue()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "escaped-commas.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "multiline-briefing.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithEscapedCommas());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("Mission With Escaped Commas", kml);
                Assert.Contains("Rain, wind, and fog are present.", kml);

                // These fragments prove the comma-containing text stayed in one value.
                Assert.DoesNotContain("Comments: Rain", kml);
                Assert.Contains("Comments:\nRain, wind, and fog are present.", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithMultilineBriefing_PreservesBriefingLines()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "multiline-briefing.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "multiline-briefing.postbrief.kml");

                CreateAcmiZip(zipPath, BuildAcmiWithMultilineBriefing());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService(weaponDatabaseMock.Object);

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = File.ReadAllText(outputPath);

                Assert.Contains("<name>Mission</name>", kml);
                Assert.Contains("Mission With Multiline Briefing", kml);
                Assert.Contains("Line one of briefing.", kml);
                Assert.Contains("Line two of briefing.", kml);
                Assert.Contains("Line three of briefing.", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static string BuildAcmiWithMissionMetadata()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.1
           0,ReferenceLongitude=141
           0,ReferenceLatitude=10
           0,ReferenceTime=2016-03-21T04:30:00Z
           0,RecordingTime=2024-07-06T17:56:08.145Z
           0,Title=UH-1_MAR_IA_Weapons Range
           0,DataRecorder=DCS2ACMI 1.9.3.200
           0,DataSource=DCS 2.9.5.55918
           0,Author=Spaz
           0,Comments=Welcome to the Mariana Islands weapons range on the island Farallon de Medinilla. The weather is overcast with rain\, but the range is open.
           40000001,T=3.7975415|3.4849998|2000,Type=Navaid+Static+Bullseye,Color=Blue,Coalition=Enemies
           40000002,T=3.7975415|3.4849998|2000,Type=Navaid+Static+Bullseye,Color=Grey,Coalition=Neutrals
           40000003,T=3.7975415|3.4849998|2000,Type=Navaid+Static+Bullseye,Color=Red,Coalition=Allies
           #0.07
           0,Category=CAS
           0,Briefing=You are FORD 2 1\, a single USMC UH-1 embarked onboard USS TARAWA (LHA-1). You have two M60 door gunners\, 2 x M-143 mini-guns and 14 x 2.75 Hydra rockets.\
           \
           Communications:\
           Range Control and LHA-1 Tower: 251.00 MHz (UHF).
           """;
        }

        private static string BuildAcmiWithEscapedCommas()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.1
           0,ReferenceLongitude=141
           0,ReferenceLatitude=10
           0,ReferenceTime=2016-03-21T04:30:00Z
           0,Title=Mission With Escaped Commas
           0,Comments=Rain\, wind\, and fog are present.
           #0.00
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Coalition=Blue,T=48.00000000|29.00000000|5000|0|0|90
           """;
        }

        private static string BuildAcmiWithMultilineBriefing()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.1
           0,ReferenceLongitude=141
           0,ReferenceLatitude=10
           0,ReferenceTime=2016-03-21T04:30:00Z
           0,Title=Mission With Multiline Briefing
           #0.00
           0,Briefing=Line one of briefing.\
           Line two of briefing.\
           Line three of briefing.
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Coalition=Blue,T=48.00000000|29.00000000|5000|0|0|90
           """;
        }

        private static string BuildAcmiWithBullseye()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2026-06-07T20:00:00Z
           #0.00
           900,Name=Blue Bullseye,Type=ReferencePoint,Group=Blue Bullseye,Coalition=Allies,T=48.00000000|29.00000000|0|0|0|0
           #1.00
           900,T=48.00000000|29.00000000|0|0|0|0
           """;
        }

        private static Mock<IWeaponDatabaseService> CreateWeaponDatabaseMock(
            IReadOnlyCollection<string> knownWeapons)
        {
            var mock = new Mock<IWeaponDatabaseService>(MockBehavior.Strict);

            mock.Setup(x => x.IsKnownWeapon(It.IsAny<string>()))
                .Returns<string>(value =>
                    !string.IsNullOrWhiteSpace(value)
                    && knownWeapons.Any(known =>
                        value.Contains(known, StringComparison.OrdinalIgnoreCase)));

            mock.Setup(x => x.GetWeaponName(It.IsAny<string>()))
                .Returns<string>(value => value);

            return mock;
        }

        private static string BuildSampleAcmi()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2026-06-07T20:00:00Z
           #0.00
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Coalition=Blue,T=48.66236111|29.96027500|1500|0|0|90
           300,Name=Target Truck,Type=Ground+Vehicle,Group=Target Group,Coalition=Red,T=48.70000000|29.98000000|0|0|0|0
           #10.00
           100,T=48.67236111|29.97027500|1600|0|0|90
           300,T=48.70000000|29.98000000|0|0|0|0
           #12.50
           200,Name=AIM-120C,Type=Weapon+Missile,Parent=100,T=48.66300000|29.96100000|1550|0|0|90
           #15.00
           200,T=48.69000000|29.97500000|500|0|0|90
           #20.00
           0,Event=Destroyed|300|Target destroyed
           """;
        }

        private static string BuildAcmiWithFuelTankAndKnownWeapon()
        {
            return """
                   FileType=text/acmi/tacview
                   FileVersion=2.2
                   0,ReferenceTime=2026-06-07T20:00:00Z
                   #0.00
                   100,Name=F-14B,Type=Air+FixedWing,Group=Colt 1,Coalition=Blue,T=48.00000000|29.00000000|7000|0|0|90
                   #5.00
                   200,Name=Fuel Tank,Type=Weapon+Container,Parent=100,T=48.00100000|29.00100000|6900|0|0|90
                   #6.00
                   200,T=48.00200000|29.00200000|6700|0|0|90
                   #10.00
                   300,Name=AIM-120C,Type=Weapon+Missile,Parent=100,T=48.00300000|29.00300000|6800|0|0|90
                   #20.00
                   0,Event=Destroyed|400|Target destroyed
                   """;
        }

        private static string BuildAcmiWithCountermeasures()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2026-06-07T20:00:00Z
           #0.00
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Coalition=Blue,T=48.00000000|29.00000000|5000|0|0|90
           #1.00
           5e01,Name=Unknown,Type=Misc+Decoy+Chaff,Parent=100,T=48.00100000|29.00100000|4990|0|0|90
           #2.00
           5e02,Name=Unknown,Type=Misc+Decoy+Flare,Parent=100,T=48.00200000|29.00200000|4980|0|0|90
           #3.00
           100,T=48.00300000|29.00300000|5000|0|0|90
           """;
        }

        private static string BuildAcmiWithTacviewRelativeAllies()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2026-06-07T20:00:00Z
           #0.00
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Coalition=Allies,T=48.00000000|29.00000000|5000|0|0|90
           #1.00
           100,T=48.00100000|29.00100000|5000|0|0|90
           """;
        }

        private static string BuildAcmiWithManyTrackPoints(int pointCount)
        {
            var builder = new System.Text.StringBuilder();

            builder.AppendLine("FileType=text/acmi/tacview");
            builder.AppendLine("FileVersion=2.2");
            builder.AppendLine("0,ReferenceTime=2026-06-07T20:00:00Z");

            for (int i = 0; i < pointCount; i++)
            {
                builder.AppendLine(FormattableString.Invariant($"#{i:0.00}"));

                if (i == 0)
                {
                    builder.AppendLine(
                        FormattableString.Invariant(
                            $"100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Coalition=Blue,T={48.0 + i * 0.001:F8}|{29.0 + i * 0.001:F8}|{1000 + i}|0|0|90"));
                }
                else
                {
                    builder.AppendLine(
                        FormattableString.Invariant(
                            $"100,T={48.0 + i * 0.001:F8}|{29.0 + i * 0.001:F8}|{1000 + i}|0|0|90"));
                }
            }

            return builder.ToString();
        }

        private static void CreateAcmiZip(string zipPath, string acmiContent)
        {
            using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            ZipArchiveEntry entry = archive.CreateEntry("sample.acmi");

            using Stream stream = entry.Open();
            using StreamWriter writer = new(stream);

            writer.Write(acmiContent);
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "DcsMissionReaderTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);

            return path;
        }
    }
}
