using DcsMissionReader.Services;
using System.IO.Compression;

namespace DcsMissionReaderTests
{
    public sealed class Issue23WeaponShooterResolutionTests
    {
        private sealed record TestObjectIdentity(string Name, string Group, string? Pilot = null)
        {
            public string DisplayName =>
                string.IsNullOrWhiteSpace(Pilot)
                    ? $"{Group}-{Name}"
                    : $"{Group}-{Pilot}";
        }

        private static readonly TestObjectIdentity CgAhead = new(
            Name: "TICONDEROG",
            Group: "CG Ahead");

        private static readonly TestObjectIdentity AlertAircraft = new(
            Name: "F-14B",
            Group: "Alert 5");

        private const string Sm2ErWeaponName = "SM_2ER";

        [Fact]
        public void CreatePostBriefingKml_WithWeaponParentShip_UsesTacviewParentAsShooter()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "issue-23-parent-ship-shooter.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "issue-23-parent-ship-shooter.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithWeaponParentShip());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(1, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains(Sm2ErWeaponName, kml);

                Assert.Contains($"Shooter: {CgAhead.DisplayName}", kml);
                Assert.Contains($"Shooter - {CgAhead.DisplayName}", kml);

                Assert.DoesNotContain($"Shooter: {AlertAircraft.DisplayName}", kml);
                Assert.DoesNotContain($"Shooter - {AlertAircraft.DisplayName}", kml);
                Assert.DoesNotContain($"{Sm2ErWeaponName} - {AlertAircraft.DisplayName}", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithWeaponMissingParent_DoesNotInferShooterFromNearbyObject()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "issue-23-missing-parent-no-shooter-guess.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "issue-23-missing-parent-no-shooter-guess.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithWeaponMissingParentNearShipAndAircraft());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(1, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains(Sm2ErWeaponName, kml);

                Assert.Contains("Shooter: Unknown", kml);

                Assert.DoesNotContain($"Shooter: {CgAhead.DisplayName}", kml);
                Assert.DoesNotContain($"Shooter - {CgAhead.DisplayName}", kml);
                Assert.DoesNotContain($"{Sm2ErWeaponName} - {CgAhead.DisplayName}", kml);

                Assert.DoesNotContain($"Shooter: {AlertAircraft.DisplayName}", kml);
                Assert.DoesNotContain($"Shooter - {AlertAircraft.DisplayName}", kml);
                Assert.DoesNotContain($"{Sm2ErWeaponName} - {AlertAircraft.DisplayName}", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static string BuildAcmiWithWeaponParentShip()
        {
            return $$"""
            FileType=text/acmi/tacview
            FileVersion=2.2
            0,ReferenceTime=2026-06-07T20:00:00Z

            #0.00
            501,Name={{CgAhead.Name}},Type=Sea+Watercraft+Warship,Group={{CgAhead.Group}},Color=Blue,Coalition=Enemies,T=57.16685430|25.54659780|0|0|0|90
            1901,Name={{AlertAircraft.Name}},Type=Air+FixedWing,Group={{AlertAircraft.Group}},Color=Blue,Coalition=Enemies,T=57.17644020|25.53037240|21.96|0|0|90

            #160.85
            1901,T=57.16843880|25.55155660|21.96|0|0|90
            6101,Name={{Sm2ErWeaponName}},Type=Weapon+Missile,Parent=501,Color=Blue,Coalition=Enemies,T=57.16623690|25.54667620|4.1|76|90|355.1

            #161.00
            6101,T=57.16616130|25.54674410|246.54|0|0|90

            #161.20
            6101,T=57.16592620|25.54695530|408.24|0|0|90
            """;
        }

        private static string BuildAcmiWithWeaponMissingParentNearShipAndAircraft()
        {
            return $$"""
            FileType=text/acmi/tacview
            FileVersion=2.2
            0,ReferenceTime=2026-06-07T20:00:00Z

            #0.00
            501,Name={{CgAhead.Name}},Type=Sea+Watercraft+Warship,Group={{CgAhead.Group}},Color=Blue,Coalition=Enemies,T=57.16685430|25.54659780|0|0|0|90
            1901,Name={{AlertAircraft.Name}},Type=Air+FixedWing,Group={{AlertAircraft.Group}},Color=Blue,Coalition=Enemies,T=57.17644020|25.53037240|21.96|0|0|90

            #160.85
            1901,T=57.16843880|25.55155660|21.96|0|0|90
            6101,Name={{Sm2ErWeaponName}},Type=Weapon+Missile,Color=Blue,Coalition=Enemies,T=57.16623690|25.54667620|4.1|76|90|355.1

            #161.00
            6101,T=57.16616130|25.54674410|246.54|0|0|90

            #161.20
            6101,T=57.16592620|25.54695530|408.24|0|0|90
            """;
        }

        private static string CreateTempDirectory()
        {
            string tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "DcsMissionInspectorTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempDirectory);

            return tempDirectory;
        }

        private static void CreateAcmiZip(string zipPath, string acmiContent)
        {
            using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            ZipArchiveEntry entry = archive.CreateEntry("mission.acmi");

            using Stream stream = entry.Open();
            using StreamWriter writer = new(stream);

            writer.Write(acmiContent);
        }

        private static string ReadKmlFromKmz(string kmzPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(kmzPath);

            ZipArchiveEntry? entry = archive.Entries
                .FirstOrDefault(e => e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(entry);

            using Stream stream = entry.Open();
            using StreamReader reader = new(stream);

            return reader.ReadToEnd();
        }
    }
}