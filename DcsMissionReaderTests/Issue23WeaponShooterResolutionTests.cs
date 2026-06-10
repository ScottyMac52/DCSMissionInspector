using DcsMissionReader.Services;
using System.IO.Compression;

namespace DcsMissionReaderTests
{
    public sealed class Issue23WeaponShooterResolutionTests
    {
        [Fact]
        public void CreatePostBriefingKml_WithWeaponParentedToNonColocatedAircraft_UsesColocatedLauncherInstead()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "issue-23-shooter-resolution.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "issue-23-shooter-resolution.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildAcmiWithWeaponParentedToNonColocatedAircraft());

                var service = new PostBriefingService();

                var result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.Equal(1, result.WeaponEmploymentCount);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("SM_2ER", kml);
                Assert.Contains("Shooter: Carrier Strike Group", kml);
                Assert.Contains("Shooter - Carrier Strike Group", kml);

                Assert.DoesNotContain("Shooter: Alert 5", kml);
                Assert.DoesNotContain("Shooter - Alert 5", kml);
                Assert.DoesNotContain("SM_2ER - Alert 5", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static string BuildAcmiWithWeaponParentedToNonColocatedAircraft()
        {
            return """
            FileType=text/acmi/tacview
            FileVersion=2.2
            0,ReferenceTime=2026-06-07T20:00:00Z
            #0.00
            100,Name=F-14B,Type=Air+FixedWing,Group=Alert 5,Color=Blue,Coalition=Enemies,T=57.16623690|25.54667620|5000|0|0|90
            200,Name=CG-60,Type=Sea+Watercraft+Cruiser,Group=Carrier Strike Group,Color=Blue,Coalition=Enemies,T=57.16623690|25.54667620|4.1|0|0|90
            #10.00
            100,T=57.16623690|25.54667620|5000|0|0|90
            200,T=57.16623690|25.54667620|4.1|0|0|90
            6101,Name=SM_2ER,Type=Weapon+Missile,Parent=100,Color=Blue,Coalition=Enemies,T=57.16623690|25.54667620|4.1|0|0|90
            #15.00
            6101,T=57.17000000|25.55000000|1000|0|0|90
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
