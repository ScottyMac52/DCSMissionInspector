using DcsMissionReader.Services;
using System.IO.Compression;

namespace DcsMissionReaderTests
{
    public sealed class Issue23WeaponShooterResolutionTests
    {
        [Fact]
        public void CreatePostBriefingKml_WithWeaponLaunchedNearPersistentShipPosition_DoesNotUseNearbyAircraftAsShooter()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "issue-23-shooter-resolution.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "issue-23-shooter-resolution.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithWeaponLaunchedNearPersistentShipPosition());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(1, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("SM_2ER", kml);
                Assert.Contains("Shooter: CG Ahead", kml);
                Assert.Contains("Shooter - CG Ahead", kml);

                Assert.DoesNotContain("Shooter: Alert 5", kml);
                Assert.DoesNotContain("Shooter - Alert 5", kml);
                Assert.DoesNotContain("SM_2ER - Alert 5", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static string BuildAcmiWithWeaponLaunchedNearPersistentShipPosition()
        {
            return """
            FileType=text/acmi/tacview
            FileVersion=2.2
            0,ReferenceTime=2026-06-07T20:00:00Z
            #0.00
            501,Name=TICONDEROG,Type=Sea+Watercraft+Warship,Group=CG Ahead,Color=Blue,Coalition=Enemies,T=57.16685430|25.54659780|0|0|0|90
            1901,Name=F-14B,Type=Air+FixedWing,Group=Alert 5,Color=Blue,Coalition=Enemies,T=57.17644020|25.53037240|21.96|0|0|90
            #160.85
            1901,T=57.16843880|25.55155660|21.96|0|0|90
            6101,Name=SM_2ER,Type=Weapon+Missile,Color=Blue,Coalition=Enemies,T=57.16623690|25.54667620|4.1|76|90|355.1
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
