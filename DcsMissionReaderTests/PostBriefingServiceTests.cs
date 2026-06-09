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
                string outputPath = Path.Combine(tempDirectory, "sample.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildSampleAcmi());


                EnsureKmlIconsAvailableForTest();

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(zipPath, result.SourceAcmiZipFilePath);
                Assert.Equal(outputPath, result.OutputKmlFilePath);
                Assert.Equal(2, result.GroupTrackCount);
                Assert.Equal(1, result.WeaponEmploymentCount);
                Assert.Equal(1, result.WeaponResultCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("<kml", kml);
                Assert.Contains("Object Tracks", kml);
                Assert.Contains("<name>Weapons</name>", kml);
                Assert.Contains("<name>Weapon Information</name>", kml);
                Assert.Contains("<name>Launch Point</name>", kml);
                Assert.Contains("<name>Weapon Track</name>", kml);
                Assert.Contains("<name>Weapon Results</name>", kml);

                Assert.DoesNotContain("<name>Weapon Employment</name>", kml);
                Assert.DoesNotContain("<name>Weapon Results and Events</name>", kml);

                Assert.Contains("Springfield 1", kml);
                Assert.DoesNotContain("Springfield 1 START", kml);
                Assert.DoesNotContain("Springfield 1 END", kml);

                Assert.Contains("AIM-120C", kml);
                Assert.Contains("Weapon Fired - AIM-120C", kml);
                Assert.Contains("AIM-120C Track", kml);
                Assert.Contains("Destroyed - Target Truck", kml);
                Assert.Contains("48.66236111,29.96027500,1500.00", kml);
                Assert.Contains("48.70000000,29.98000000,0.00", kml);

            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithValidZippedAcmi_CreatesKmlWithTracksWeaponsAndResults()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "fuel-tank-test.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "fuel-tank-test.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithFuelTankAndKnownWeapon());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));

                // Only AIM-120C should be treated as weapon employment.
                // Fuel tanks should not become weapon employments.
                Assert.Equal(1, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

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
                string outputPath = Path.Combine(tempDirectory, "many-points.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithManyTrackPoints(pointCount: 20));

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService();

                var options = new DcsMissionReader.Models.PostBriefingKmlOptions
                {
                    MaxTrackPointsPerObject = 5
                };

                service.CreatePostBriefingKml(zipPath, outputPath, options);

                string kml = ReadKmlFromKmz(outputPath);

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

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath);

                Assert.True(File.Exists(result.OutputKmlFilePath));
                Assert.EndsWith("mission.postbrief.kmz", result.OutputKmlFilePath);
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

            var service = new PostBriefingService();

            Assert.Throws<FileNotFoundException>(() =>
                service.CreatePostBriefingKml(@"C:\does-not-exist\missing.acmi.zip"));
        }

        [Fact]
        public void CreatePostBriefingKml_WithEmptyPath_ThrowsArgumentException()
        {
            Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                knownWeapons: []);

            var service = new PostBriefingService();

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

                var service = new PostBriefingService();

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
                string outputPath = Path.Combine(tempDirectory, "countermeasures.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithCountermeasures());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: ["AIM-120C"]);

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));

                // Only the aircraft should remain as a group track.
                // Chaff and flares should not become tracks.
                Assert.Equal(1, result.GroupTrackCount);

                // No known weapons were fired in this ACMI sample.
                Assert.Equal(0, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

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
        public void CreatePostBriefingKml_WithGunShellProjectiles_DoesNotCreateWeaponEmployments()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "gun-shells.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "gun-shells.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithGunShellProjectiles());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));

                // Aircraft remains as an object track, but individual gun rounds should not become weapon employments.
                Assert.Equal(1, result.GroupTrackCount);
                Assert.Equal(0, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("Rotary-1", kml);
                Assert.DoesNotContain("weapons.shells.M61_20_HE_gr", kml);
                Assert.DoesNotContain("Weapon Fired - weapons.shells.M61_20_HE_gr", kml);
                Assert.DoesNotContain("Weapon Kind: bullet", kml);
                Assert.DoesNotContain("<name>Weapons</name>\r\n<Folder>\r\n<name>weapons.shells", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithRotorcraftNameContainingSh_DoesNotRenderAsSam()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "rotorcraft-sh60.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "rotorcraft-sh60.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithSh60Rotorcraft());

                var service = new PostBriefingService();

                service.CreatePostBriefingKml(zipPath, outputPath, new PostBriefingKmlOptions
                {
                    TreatTacviewAlliesAsBlue = false,
                    TreatTacviewEnemiesAsRed = false,
                    InferBlueForKnownUsNavalAssets = false
                });

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("Rotary-1", kml);
                Assert.Contains("SH-60B", kml);
                Assert.Contains("#blueHeloStartStyle", kml);
                Assert.DoesNotContain("#blueSamStartStyle", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithWeaponTimeout_HidesTimeoutResultPlacemark()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "weapon-timeout.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "weapon-timeout.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithWeaponTimeout());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(1, result.WeaponEmploymentCount);
                Assert.Equal(1, result.WeaponResultCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("Timeout - SeaSparrow", kml);
                AssertPlacemarkVisibility(kml, "Timeout - SeaSparrow", expectedVisibility: "0");
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
                string outputPath = Path.Combine(tempDirectory, "allies-relative.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithTacviewRelativeAllies());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService();

                var options = new PostBriefingKmlOptions
                {
                    TreatTacviewAlliesAsBlue = false,
                    TreatTacviewEnemiesAsRed = false,
                    InferBlueForKnownUsNavalAssets = false
                };

                service.CreatePostBriefingKml(zipPath, outputPath, options);

                string kml = ReadKmlFromKmz(outputPath);

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
                string outputPath = Path.Combine(tempDirectory, "bullseye.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithBullseye());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService();

                service.CreatePostBriefingKml(zipPath, outputPath, new PostBriefingKmlOptions
                {
                    TreatTacviewAlliesAsBlue = false,
                    TreatTacviewEnemiesAsRed = false,
                    InferBlueForKnownUsNavalAssets = true
                });

                string kml = ReadKmlFromKmz(outputPath);

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
        public void CreatePostBriefingKml_WithFixedWingGroupNameContainingCarrier_RendersAsPlaneNotShip()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "carrier-killer-fixed-wing.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "carrier-killer-fixed-wing.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithFixedWingCarrierKillerGroup());

                var service = new PostBriefingService();

                service.CreatePostBriefingKml(zipPath, outputPath, new PostBriefingKmlOptions
                {
                    TreatTacviewAlliesAsBlue = false,
                    TreatTacviewEnemiesAsRed = false,
                    InferBlueForKnownUsNavalAssets = false
                });

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("Tu-22M3", kml);
                Assert.Contains("Carrier Killer", kml);
                Assert.Contains("#redPlaneStartStyle", kml);
                Assert.DoesNotContain("#redShipStartStyle", kml);
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
                string outputPath = Path.Combine(tempDirectory, "mission-info.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithMissionMetadata());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(outputPath, result.OutputKmlFilePath);

                string kml = ReadKmlFromKmz(outputPath);

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
                string outputPath = Path.Combine(tempDirectory, "escaped-commas.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithEscapedCommas());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService();

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = ReadKmlFromKmz(outputPath);

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
                string outputPath = Path.Combine(tempDirectory, "multiline-briefing.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithMultilineBriefing());

                Mock<IWeaponDatabaseService> weaponDatabaseMock = CreateWeaponDatabaseMock(
                    knownWeapons: []);

                var service = new PostBriefingService();

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = ReadKmlFromKmz(outputPath);

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


        [Fact]
        public void CreatePostBriefingKml_WithKmzOutput_EmbedsDocKmlAndWeaponIcons()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "icons-test.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "icons-test.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildSampleAcmi());

                EnsureKmlIconsAvailableForTest();

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(result.OutputKmlFilePath));
                Assert.Equal(outputPath, result.OutputKmlFilePath);

                using ZipArchive archive = ZipFile.OpenRead(result.OutputKmlFilePath);

                Assert.NotNull(archive.GetEntry("doc.kml"));
                Assert.NotNull(archive.GetEntry("icons/missile.png"));
                Assert.NotNull(archive.GetEntry("icons/bomb.png"));
                Assert.NotNull(archive.GetEntry("icons/sam.png"));
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithSamSiteAndLaunchedSam_UsesSamIconStyles()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "sam-test.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "sam-test.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithSamSiteAndLaunchedSam());
                CreateTestKmlIcons(tempDirectory);

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(result.OutputKmlFilePath));

                string kml = ReadKmlFromKmz(result.OutputKmlFilePath);

                Assert.Contains("#redSamStartStyle", kml);
                Assert.Contains("#weaponEmploymentSamStyle", kml);
                Assert.Contains("icons/sam.png", kml);
                Assert.Contains("Weapon Kind: sam", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithUnknownTacviewWeapon_IncludesWeaponEmployment()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "unknown-weapon.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "unknown-weapon.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithUnknownTacviewWeapon());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(1, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("Mystery Missile", kml);
                Assert.Contains("Weapon Fired - Mystery Missile", kml);
                Assert.Contains("<name>Launching Unit</name>", kml);
                Assert.Contains("Launching Unit - Springfield 1", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithDestroyedTarget_AddsDispositionToTargetObjectDescription()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "target-disposition.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "target-disposition.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithWeaponKillAndHealth());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(2, result.GroupTrackCount);
                Assert.Equal(1, result.WeaponEmploymentCount);
                Assert.Equal(1, result.WeaponResultCount);

                string kml = ReadKmlFromKmz(outputPath);

                AssertPlacemarkDescriptionContains(
                    kml,
                    "Overlord",
                    "Final Disposition:\nDestroyed");

                AssertPlacemarkDescriptionContains(
                    kml,
                    "Overlord",
                    "Health Remaining: 0%");

                AssertPlacemarkDescriptionContains(
                    kml,
                    "Overlord",
                    "Weapons That Hit / Destroyed This Object:\n- P_33E from AWACS KILLER");
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithSurvivingObject_AddsSurvivedDispositionToObjectDescription()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "survivor-disposition.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "survivor-disposition.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithSurvivingObjectHealth());

                var service = new PostBriefingService();

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = ReadKmlFromKmz(outputPath);

                AssertPlacemarkDescriptionContains(
                    kml,
                    "Springfield 1",
                    "Health Remaining: 100%");

                AssertPlacemarkDescriptionContains(
                    kml,
                    "Springfield 1",
                    "Final Disposition:\nSurvived / No Weapon Result Recorded");
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static void AssertPlacemarkDescriptionContains(
            string kml,
            string placemarkName,
            string expectedDescriptionText)
        {
            string nameElement = $"<name>{placemarkName}</name>";

            int searchIndex = 0;

            while (true)
            {
                int placemarkStart = kml.IndexOf("<Placemark>", searchIndex, StringComparison.Ordinal);

                if (placemarkStart < 0)
                {
                    break;
                }

                int placemarkEnd = kml.IndexOf("</Placemark>", placemarkStart, StringComparison.Ordinal);

                Assert.True(
                    placemarkEnd > placemarkStart,
                    $"Found opening Placemark but no closing Placemark while searching for: {placemarkName}");

                string placemark = kml.Substring(
                    placemarkStart,
                    placemarkEnd + "</Placemark>".Length - placemarkStart);

                if (placemark.Contains(nameElement, StringComparison.Ordinal))
                {
                    string normalizedPlacemark = NormalizeLineEndings(placemark);
                    string normalizedExpected = NormalizeLineEndings(expectedDescriptionText);

                    Assert.Contains(normalizedExpected, normalizedPlacemark);
                    return;
                }

                searchIndex = placemarkEnd + "</Placemark>".Length;
            }

            Assert.Fail($"Could not find Placemark with name: {placemarkName}");
        }

        private static string NormalizeLineEndings(string value)
        {
            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
        }
                

        private static string BuildAcmiWithWeaponKillAndHealth()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z
           #0.00
           100,Name=MiG-31,Type=Air+FixedWing,Group=AWACS KILLER,Color=Red,Coalition=Allies,T=48.00000000|29.00000000|10000|0|0|90,Health=1
           300,Name=E-2C,Type=Air+FixedWing,Group=Overlord,Color=Blue,Coalition=Enemies,T=48.50000000|29.50000000|9000|0|0|270,Health=1
           #10.00
           200,Name=P_33E,Type=Weapon+Missile,Parent=100,Color=Red,Coalition=Allies,T=48.01000000|29.01000000|10000|0|0|90
           #20.00
           200,T=48.25000000|29.25000000|9500|0|0|90
           #30.00
           300,Health=0
           200,T=48.50000000|29.50000000|9000|0|0|90
           -200
           -300
           """;
        }

        private static string BuildAcmiWithSurvivingObjectHealth()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z
           #0.00
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Color=Blue,Coalition=Enemies,T=48.00000000|29.00000000|5000|0|0|90,Health=1
           #10.00
           100,T=48.01000000|29.01000000|5100|0|0|90
           """;
        }

        private static string BuildAcmiWithSh60Rotorcraft()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z
           #0.01
           1301,Name=SH-60B,Type=Air+Rotorcraft,Group=Rotary-1,Color=Blue,Coalition=Enemies,T=57.17663780|25.53163180|498.45|0|0|90
           #1.00
           1301,T=57.17670000|25.53170000|500.00|0|0|90
           """;
        }

        private static string BuildAcmiWithWeaponTimeout()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z
           #0.00
           100,Name=CVN-73,Type=Sea+Watercraft,Group=USS Washington,Color=Blue,Coalition=Enemies,T=48.00000000|29.00000000|0|0|0|0
           #10.00
           200,Name=SeaSparrow,Type=Weapon+Missile,Parent=100,Color=Blue,Coalition=Enemies,T=48.00100000|29.00100000|100|0|0|90
           #15.00
           200,T=48.01000000|29.01000000|500|0|0|90
           #20.00
           -200
           """;
        }

        private static string BuildAcmiWithFixedWingCarrierKillerGroup()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z
           #0.01
           e01,Name=Tu-22M3,Type=Air+FixedWing,Group=Carrier Killer,Color=Red,Coalition=Allies,T=48.00000000|29.00000000|5000|0|0|90
           #1.00
           e01,T=48.01000000|29.01000000|5000|0|0|90
           """;
        }

        private static string BuildAcmiWithGunShellProjectiles()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z
           #0.00
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Rotary-1,Color=Blue,Coalition=Enemies,T=48.00000000|29.00000000|5000|0|0|90
           #455.55
           3019101,Name=weapons.shells.M61_20_HE_gr,Type=Projectile+Shell,Parent=100,Color=Blue,Coalition=Enemies,T=48.00100000|29.00100000|4990|0|0|90
           #455.60
           3019101,T=48.00110000|29.00110000|4980|0|0|90
           #455.65
           3019101,T=48.00120000|29.00120000|4970|0|0|90
           #455.70
           -3019101
           """;
        }

        private static string BuildAcmiWithUnknownTacviewWeapon()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2026-06-07T20:00:00Z
           #0.00
           100,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Coalition=Blue,T=48.00000000|29.00000000|5000|0|0|90
           #5.00
           200,Name=Mystery Missile,Type=Weapon+Missile,Parent=100,T=48.00100000|29.00100000|4900|0|0|90
           #6.00
           200,T=48.01000000|29.01000000|4000|0|0|90
           """;
        }

        private static void CreateTestKmlIcons(string tempDirectory)
        {
            string iconDirectory = Path.Combine(
                Environment.CurrentDirectory,
                "Data",
                "KmlIcons");

            Directory.CreateDirectory(iconDirectory);

            File.WriteAllBytes(
                Path.Combine(iconDirectory, "missile.png"),
                CreateMinimalPngBytes());

            File.WriteAllBytes(
                Path.Combine(iconDirectory, "bomb.png"),
                CreateMinimalPngBytes());

            File.WriteAllBytes(
                Path.Combine(iconDirectory, "sam.png"),
                CreateMinimalPngBytes());
        }

        private static byte[] CreateMinimalPngBytes()
        {
            // 1x1 transparent PNG.
            return Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        }

        private static string BuildAcmiWithSamSiteAndLaunchedSam()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2026-06-07T20:00:00Z
           #0.00
           100,Name=SA-10 Site,Type=Ground+Vehicle+SAM,Group=SA-10 Battery,Color=Red,Coalition=Red,T=48.00000000|29.00000000|0|0|0|0
           200,Name=F/A-18C,Type=Air+FixedWing,Group=Springfield 1,Color=Blue,Coalition=Blue,T=48.10000000|29.10000000|5000|0|0|90
           #5.00
           300,Name=SA-10 Missile,Type=Weapon+Missile,Parent=100,Color=Red,Coalition=Red,T=48.00010000|29.00010000|100|0|0|90
           #10.00
           300,T=48.05000000|29.05000000|3000|0|0|90
           """;
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



        private static void EnsureKmlIconsAvailableForTest()
        {
            string iconDirectory = Path.Combine(Environment.CurrentDirectory, "Data", "KmlIcons");

            Directory.CreateDirectory(iconDirectory);

            WritePlaceholderIconIfMissing(Path.Combine(iconDirectory, "missile.png"));
            WritePlaceholderIconIfMissing(Path.Combine(iconDirectory, "bomb.png"));
            WritePlaceholderIconIfMissing(Path.Combine(iconDirectory, "sam.png"));
        }

        private static void WritePlaceholderIconIfMissing(string path)
        {
            if (File.Exists(path))
            {
                return;
            }

            // 1x1 transparent PNG.
            byte[] png = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
                0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
                0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                0x42, 0x60, 0x82
            };

            File.WriteAllBytes(path, png);
        }

        private static string ReadKmlFromKmz(string kmzPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(kmzPath);

            ZipArchiveEntry? kmlEntry = archive.GetEntry("doc.kml");

            Assert.NotNull(kmlEntry);

            using Stream stream = kmlEntry.Open();
            using StreamReader reader = new(stream);

            return reader.ReadToEnd();
        }

        private static void AssertPlacemarkVisibility(
            string kml,
            string placemarkName,
            string expectedVisibility)
        {
            string nameElement = $"<name>{placemarkName}</name>";
            int nameIndex = kml.IndexOf(nameElement, StringComparison.Ordinal);

            Assert.True(nameIndex >= 0, $"Could not find placemark name: {placemarkName}");

            int placemarkStart = kml.LastIndexOf("<Placemark>", nameIndex, StringComparison.Ordinal);
            int placemarkEnd = kml.IndexOf("</Placemark>", nameIndex, StringComparison.Ordinal);

            Assert.True(placemarkStart >= 0, $"Could not find opening Placemark for: {placemarkName}");
            Assert.True(placemarkEnd > placemarkStart, $"Could not find closing Placemark for: {placemarkName}");

            string placemark = kml.Substring(
                placemarkStart,
                placemarkEnd + "</Placemark>".Length - placemarkStart);

            Assert.Contains($"<visibility>{expectedVisibility}</visibility>", placemark);
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
