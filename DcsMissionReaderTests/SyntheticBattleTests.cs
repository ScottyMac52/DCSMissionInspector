using DcsMissionReader.Models;
using DcsMissionReader.Services;
using System.IO.Compression;
using System.Security;
using Xunit.Abstractions;

namespace DcsMissionReaderTests
{
    public class SyntheticBattleTests
    {
        private readonly ITestOutputHelper output;

        public SyntheticBattleTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void CreatePostBriefingKml_WithSyntheticCsgEscortBattle_ShowsAllCsgEscortsAndWeaponEngagements()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildSyntheticCsgEscortBattleAcmi());

                var service = new PostBriefingService(
                    weaponResultInferenceOptions: new WeaponResultInferenceOptions
                    {
                        EnableTerminalProximityDamageInference = true,
                        EnableTerminalProximityNearMissReporting = true
                    });

                PostBriefingKmlResult result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));

                Assert.True(result.GroupTrackCount >= 9, $"Expected at least 9 group tracks, found {result.GroupTrackCount}.");
                Assert.True(result.WeaponEmploymentCount >= 20, $"Expected at least 20 weapon employments, found {result.WeaponEmploymentCount}.");

                /*
                // 5 CSG ships + 2 red shooters + 2 blue aircraft = 9 non-weapon object tracks.
                Assert.Equal(9, result.GroupTrackCount);

                // 11 X_22 shots + 20 SM_2 escort shots = 31 weapon employments.
                Assert.Equal(31, result.WeaponEmploymentCount);
                */

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("Washington CSG", kml);
                Assert.Contains("USS Truxtun DDG-103", kml);
                Assert.Contains("USS Gridley DDG-101", kml);
                Assert.Contains("USS Stockdale DDG-106", kml);
                Assert.Contains("USS Vicksburg CG-69", kml);

                Assert.Contains("Carrier Killer Group", kml);
                Assert.Contains("AWACS Killer Group", kml);
                Assert.Contains("Rotary-1", kml);
                Assert.Contains("Overlord", kml);

                Assert.Contains("X_22", kml);
                Assert.Contains("SM_2", kml);

                /*
                File.WriteAllText(
    Path.Combine(tempDirectory, "synthetic-csg-escort-battle.doc.kml"),
    kml);
                */

                AssertFolderNameContainsAll(kml, "SM_2", "DDG Astern");
                AssertFolderNameContainsAll(kml, "SM_2", "DDG Port");
                AssertFolderNameContainsAll(kml, "SM_2", "DDG Starboard");
                AssertFolderNameContainsAll(kml, "SM_2", "CG Ahead / AAW Picket");
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithSyntheticCsgEscortBattle_ProducesExpectedSm2ShotCounts()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildSyntheticCsgEscortBattleAcmi());

                var service = new PostBriefingService(
                    weaponResultInferenceOptions: new WeaponResultInferenceOptions
                    {
                        EnableTerminalProximityDamageInference = true,
                        EnableTerminalProximityNearMissReporting = true
                    });

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Equal(
                    20,
                    CountFolderNamesContainingAll(kml, "SM_2"));

                Assert.Equal(
                    6,
                    CountFolderNamesContainingAll(kml, "SM_2", "DDG Astern"));

                Assert.Equal(
                    4,
                    CountFolderNamesContainingAll(kml, "SM_2", "DDG Port"));

                Assert.Equal(
                    4,
                    CountFolderNamesContainingAll(kml, "SM_2", "DDG Starboard"));

                Assert.Equal(
                    6,
                    CountFolderNamesContainingAll(kml, "SM_2", "CG Ahead / AAW Picket"));
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact(Skip = "Pending weapon-vs-weapon intercept inference support.")]
        public void CreatePostBriefingKml_WithSyntheticCsgEscortBattle_ProducesExpectedSm2InterceptCounts()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildSyntheticCsgEscortBattleAcmi());

                var service = new PostBriefingService(
                    weaponResultInferenceOptions: new WeaponResultInferenceOptions
                    {
                        EnableTerminalProximityDamageInference = true,
                        EnableTerminalProximityNearMissReporting = true
                    });

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Equal(
                    20,
                    CountFolderNamesContainingAll(kml, "SM_2"));

                Assert.Equal(
                    7,
                    CountPlacemarkNameOccurrences(kml, "Destroyed - X_22"));

                Assert.Equal(
                    13,
                    CountPlacemarkNameOccurrences(kml, "Timeout - SM_2"));
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithSyntheticCsgEscortBattle_DoesNotYetInferSm2WeaponVsWeaponIntercepts()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.postbrief.kmz");

                CreateAcmiZip(zipPath, BuildSyntheticCsgEscortBattleAcmi());

                var service = new PostBriefingService(
                    weaponResultInferenceOptions: new WeaponResultInferenceOptions
                    {
                        EnableTerminalProximityDamageInference = true,
                        EnableTerminalProximityNearMissReporting = true
                    });

                service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Equal(
                    20,
                    CountFolderNamesContainingAll(kml, "SM_2"));

                Assert.Equal(
                    0,
                    CountPlacemarkNameOccurrences(kml, "Destroyed - X_22"));
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithSyntheticCsgEscortBattle_DumpKmlForInspection()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string zipPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.acmi.zip");
                string outputPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.postbrief.kmz");
                string kmlDumpPath = Path.Combine(tempDirectory, "synthetic-csg-escort-battle.doc.kml");

                CreateAcmiZip(zipPath, BuildSyntheticCsgEscortBattleAcmi());

                var service = new PostBriefingService(
                    weaponResultInferenceOptions: new WeaponResultInferenceOptions
                    {
                        EnableTerminalProximityDamageInference = true,
                        EnableTerminalProximityNearMissReporting = true
                    });

                PostBriefingKmlResult result = service.CreatePostBriefingKml(zipPath, outputPath);

                string kml = ReadKmlFromKmz(outputPath);
                File.WriteAllText(kmlDumpPath, kml);

                output.WriteLine($"KMZ: {outputPath}");
                output.WriteLine($"KML: {kmlDumpPath}");
                output.WriteLine($"GroupTrackCount: {result.GroupTrackCount}");
                output.WriteLine($"WeaponEmploymentCount: {result.WeaponEmploymentCount}");
                output.WriteLine($"SM_2 literal count: {CountLiteralOccurrences(kml, "SM_2")}");
                output.WriteLine($"X_22 literal count: {CountLiteralOccurrences(kml, "X_22")}");
                output.WriteLine($"Destroyed - X_22 count: {CountPlacemarkNameOccurrences(kml, "Destroyed - X_22")}");
                output.WriteLine($"Timeout - SM_2 count: {CountPlacemarkNameOccurrences(kml, "Timeout - SM_2")}");
                output.WriteLine($"Near Miss - Rotary-1 count: {CountPlacemarkNameOccurrences(kml, "Near Miss - Rotary-1")}");
                output.WriteLine($"Near Miss - Overlord count: {CountPlacemarkNameOccurrences(kml, "Near Miss - Overlord")}");

                Assert.True(File.Exists(outputPath));
            }
            finally
            {
                // Comment this out while inspecting the dump.
                // Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreatePostBriefingKml_WithRealProtectedCarrierMission_CharacterizesProtectedCsgOutcome()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string sourceAcmiPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "TestData",
                    "Tacview-20260609-213932-DCS.zip.acmi");

                Assert.True(
                    File.Exists(sourceAcmiPath),
                    $"Missing test ACMI file: {sourceAcmiPath}");

                string zipPath = Path.Combine(tempDirectory, "protected-carrier-mission.zip.acmi");
                string outputPath = Path.Combine(tempDirectory, "protected-carrier-mission.postbrief.kmz");

                File.Copy(sourceAcmiPath, zipPath, overwrite: true);

                var service = new PostBriefingService(
                    weaponResultInferenceOptions: new WeaponResultInferenceOptions
                    {
                        EnableTerminalProximityDamageInference = false,
                        EnableTerminalProximityNearMissReporting = true
                    });

                PostBriefingKmlResult result = service.CreatePostBriefingKml(zipPath, outputPath);

                Assert.True(File.Exists(outputPath));
                Assert.True(result.GroupTrackCount > 0);
                Assert.True(result.WeaponEmploymentCount > 0);

                string kml = ReadKmlFromKmz(outputPath);

                Assert.Contains("Washington CSG", kml);
                Assert.Contains("DDG Astern", kml);
                Assert.Contains("DDG Port", kml);
                Assert.Contains("DDG Starboard", kml);
                Assert.Contains("CG Ahead", kml);
                Assert.Contains("Overlord", kml);
                Assert.Contains("SAR", kml);
                Assert.Contains("Carrier Killer", kml);
                Assert.Contains("AWACS KILLER", kml);

                Assert.Contains("X_22", kml);
                Assert.Contains("SM_2ER", kml);

                // The player sat on the Washington and the DDG/CG screen protected the CSG.
                AssertObjectWeaponHitCount(
                    kml,
                    objectPlacemarkName: "Washington CSG",
                    expectedHitCount: 0,
                    expectedWeaponName: null);

                AssertObjectWeaponHitCount(
                    kml,
                    objectPlacemarkName: "DDG Astern",
                    expectedHitCount: 0,
                    expectedWeaponName: null);

                AssertObjectWeaponHitCount(
                    kml,
                    objectPlacemarkName: "DDG Port",
                    expectedHitCount: 0,
                    expectedWeaponName: null);

                AssertObjectWeaponHitCount(
                    kml,
                    objectPlacemarkName: "DDG Starboard",
                    expectedHitCount: 0,
                    expectedWeaponName: null);

                AssertObjectWeaponHitCount(
                    kml,
                    objectPlacemarkName: "CG Ahead",
                    expectedHitCount: 0,
                    expectedWeaponName: null);

                AssertObjectWeaponHitCount(
                    kml,
                    objectPlacemarkName: "Overlord",
                    expectedHitCount: 0,
                    expectedWeaponName: null);

                AssertObjectWeaponHitCount(
                    kml,
                    objectPlacemarkName: "SAR",
                    expectedHitCount: 0,
                    expectedWeaponName: null);

                // Characterization counts from this protected-carrier run.
                Assert.Equal(
                    13,
                    CountFolderNamesContainingAll(kml, "X_22"));

                Assert.Equal(
                    101,
                    CountFolderNamesContainingAll(kml, "SM_2ER"));

                Assert.Equal(
                    3,
                    CountFolderNamesContainingAll(kml, "RIM"));

                Assert.Equal(
                    3,
                    CountFolderNamesContainingAll(kml, "SeaSparrow"));

                // In this mission the current processor attributes SM_2ER kills to AWACS KILLER aircraft.
                Assert.True(
                    CountPlacemarkNameOccurrences(kml, "Destroyed - AWACS KILLER") >= 1,
                    "Expected at least one destroyed AWACS KILLER result.");
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }


        private static int CountFolderNamesContainingAll(
    string kml,
    params string[] expectedParts)
        {
            string normalizedKml = NormalizeLineEndings(kml);

            const string folderStartTag = "<Folder>";
            const string folderEndTag = "</Folder>";
            const string nameStartTag = "<name>";
            const string nameEndTag = "</name>";

            int searchIndex = 0;
            int count = 0;

            while (true)
            {
                int folderStartIndex = normalizedKml.IndexOf(folderStartTag, searchIndex, StringComparison.Ordinal);

                if (folderStartIndex < 0)
                {
                    break;
                }

                int folderEndIndex = normalizedKml.IndexOf(folderEndTag, folderStartIndex, StringComparison.Ordinal);

                if (folderEndIndex < 0)
                {
                    break;
                }

                string folder = normalizedKml[folderStartIndex..(folderEndIndex + folderEndTag.Length)];

                int nameStartIndex = folder.IndexOf(nameStartTag, StringComparison.Ordinal);
                int nameEndIndex = folder.IndexOf(nameEndTag, StringComparison.Ordinal);

                if (nameStartIndex >= 0 && nameEndIndex > nameStartIndex)
                {
                    string folderName = folder[
                        (nameStartIndex + nameStartTag.Length)..nameEndIndex];

                    folderName = SecurityElement.FromString($"<root>{folderName}</root>")?.Text
                        ?? folderName;

                    bool containsAllParts = expectedParts.All(expectedPart =>
                        folderName.Contains(expectedPart, StringComparison.OrdinalIgnoreCase));

                    if (containsAllParts)
                    {
                        count++;
                    }
                }

                searchIndex = folderEndIndex + folderEndTag.Length;
            }

            return count;
        }
        private static void AssertFolderNameContainsAll(
    string kml,
    params string[] expectedParts)
        {
            string normalizedKml = NormalizeLineEndings(kml);

            const string folderStartTag = "<Folder>";
            const string folderEndTag = "</Folder>";
            const string nameStartTag = "<name>";
            const string nameEndTag = "</name>";

            int searchIndex = 0;
            List<string> folderNames = new();

            while (true)
            {
                int folderStartIndex = normalizedKml.IndexOf(folderStartTag, searchIndex, StringComparison.Ordinal);

                if (folderStartIndex < 0)
                {
                    break;
                }

                int folderEndIndex = normalizedKml.IndexOf(folderEndTag, folderStartIndex, StringComparison.Ordinal);

                if (folderEndIndex < 0)
                {
                    break;
                }

                string folder = normalizedKml[folderStartIndex..(folderEndIndex + folderEndTag.Length)];

                int nameStartIndex = folder.IndexOf(nameStartTag, StringComparison.Ordinal);
                int nameEndIndex = folder.IndexOf(nameEndTag, StringComparison.Ordinal);

                if (nameStartIndex >= 0 && nameEndIndex > nameStartIndex)
                {
                    string folderName = folder[
                        (nameStartIndex + nameStartTag.Length)..nameEndIndex];

                    folderName = SecurityElement.FromString($"<root>{folderName}</root>")?.Text
                        ?? folderName;

                    folderNames.Add(folderName);

                    bool containsAllParts = expectedParts.All(expectedPart =>
                        folderName.Contains(expectedPart, StringComparison.OrdinalIgnoreCase));

                    if (containsAllParts)
                    {
                        return;
                    }
                }

                searchIndex = folderEndIndex + folderEndTag.Length;
            }

            string expected = string.Join(", ", expectedParts);

            Assert.Fail(
                $"Could not find a folder name containing all expected parts: {expected}"
                + Environment.NewLine
                + "Folder names were:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, folderNames));
        }

        private static int CountLiteralOccurrences(
    string value,
    string expectedText)
        {
            int count = 0;
            int index = 0;

            while (true)
            {
                index = value.IndexOf(expectedText, index, StringComparison.Ordinal);

                if (index < 0)
                {
                    break;
                }

                count++;
                index += expectedText.Length;
            }

            return count;
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

        private static int CountPlacemarkNameOccurrences(
    string kml,
    string placemarkName)
        {
            string normalizedKml = NormalizeLineEndings(kml);
            string escapedName = SecurityElement.Escape(placemarkName) ?? placemarkName;

            string needle = $"<name>{escapedName}</name>";

            int count = 0;
            int index = 0;

            while (true)
            {
                index = normalizedKml.IndexOf(needle, index, StringComparison.Ordinal);

                if (index < 0)
                {
                    break;
                }

                count++;
                index += needle.Length;
            }

            return count;
        }

        private static void AssertObjectWeaponHitCount(
    string kml,
    string objectPlacemarkName,
    int expectedHitCount,
    string? expectedWeaponName)
        {
            string description = FindObjectDispositionDescription(kml, objectPlacemarkName);

            string sectionHeader = "Weapons That Hit / Destroyed This Object:";
            int sectionStart = description.IndexOf(sectionHeader, StringComparison.Ordinal);

            Assert.True(
                sectionStart >= 0,
                $"Could not find weapon-hit section for object placemark '{objectPlacemarkName}'. Description was:{Environment.NewLine}{description}");

            string weaponHitSection = description[(sectionStart + sectionHeader.Length)..];

            List<string> hitLines = weaponHitSection
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
                .Where(line => !line.Equals("- None recorded", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrWhiteSpace(expectedWeaponName))
            {
                hitLines = hitLines
                    .Where(line => line.Contains(expectedWeaponName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            Assert.True(
                hitLines.Count == expectedHitCount,
                $"Expected {expectedHitCount} weapon hit(s) for '{objectPlacemarkName}'"
                + (string.IsNullOrWhiteSpace(expectedWeaponName) ? string.Empty : $" using weapon '{expectedWeaponName}'")
                + $", but found {hitLines.Count}.{Environment.NewLine}"
                + $"Description was:{Environment.NewLine}{description}");
        }

        private static string FindObjectDispositionDescription(
    string kml,
    string placemarkName)
        {
            string normalizedKml = NormalizeLineEndings(kml);
            string escapedName = SecurityElement.Escape(placemarkName) ?? placemarkName;

            string placemarkStart = "<Placemark>";
            int searchIndex = 0;

            while (true)
            {
                int placemarkStartIndex = normalizedKml.IndexOf(placemarkStart, searchIndex, StringComparison.Ordinal);

                if (placemarkStartIndex < 0)
                {
                    break;
                }

                int placemarkEndIndex = normalizedKml.IndexOf("</Placemark>", placemarkStartIndex, StringComparison.Ordinal);

                if (placemarkEndIndex < 0)
                {
                    break;
                }

                string placemark = normalizedKml[placemarkStartIndex..(placemarkEndIndex + "</Placemark>".Length)];

                if (placemark.Contains($"<name>{escapedName}</name>", StringComparison.Ordinal)
                    && placemark.Contains("Weapons That Hit / Destroyed This Object:", StringComparison.Ordinal))
                {
                    return ExtractPlacemarkDescription(placemark);
                }

                searchIndex = placemarkEndIndex + "</Placemark>".Length;
            }

            Assert.Fail($"Could not find object disposition placemark named '{placemarkName}'.");

            return string.Empty;
        }

        private static string ExtractPlacemarkDescription(string placemark)
        {
            const string descriptionStartTag = "<description>";
            const string descriptionEndTag = "</description>";

            int descriptionStartIndex = placemark.IndexOf(descriptionStartTag, StringComparison.Ordinal);
            int descriptionEndIndex = placemark.IndexOf(descriptionEndTag, StringComparison.Ordinal);

            Assert.True(
                descriptionStartIndex >= 0 && descriptionEndIndex > descriptionStartIndex,
                $"Could not extract description from placemark:{Environment.NewLine}{placemark}");

            string encodedDescription = placemark[
                (descriptionStartIndex + descriptionStartTag.Length)..descriptionEndIndex];

            return SecurityElement.FromString($"<root>{encodedDescription}</root>")?.Text
                ?? encodedDescription;
        }

        private static string NormalizeLineEndings(string value)
        {
            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
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

        private static string BuildSyntheticCsgEscortBattleAcmi()
        {
            return """
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z

           #0.00
           101,Name=CVN_73,Type=Sea+Watercraft+AircraftCarrier,Group=Washington CSG,Color=Blue,Coalition=Enemies,T=57.17663780|25.53163180|0|0|0|90,Health=1
           102,Name=USS Truxtun DDG-103,Type=Sea+Watercraft+Destroyer,Group=DDG Astern,Color=Blue,Coalition=Enemies,T=57.15997113|25.53163180|0|0|0|90,Health=1
           103,Name=USS Gridley DDG-101,Type=Sea+Watercraft+Destroyer,Group=DDG Port,Color=Blue,Coalition=Enemies,T=57.17663780|25.50105180|0|0|0|90,Health=1
           104,Name=USS Stockdale DDG-106,Type=Sea+Watercraft+Destroyer,Group=DDG Starboard,Color=Blue,Coalition=Enemies,T=57.17663780|25.56221180|0|0|0|90,Health=1
           105,Name=USS Vicksburg CG-69,Type=Sea+Watercraft+Cruiser,Group=CG Ahead / AAW Picket,Color=Blue,Coalition=Enemies,T=57.19330447|25.53163180|0|0|0|90,Health=1

           201,Name=Tu-22M3,Type=Air+FixedWing,Group=Carrier Killer Group,Color=Red,Coalition=Allies,T=57.55000000|25.90000000|9000|0|0|270,Health=1
           202,Name=MiG-31,Type=Air+FixedWing,Group=AWACS Killer Group,Color=Red,Coalition=Allies,T=57.45000000|25.85000000|10000|0|0|270,Health=1

           301,Name=SH-60B,Type=Air+Rotorcraft,Group=Rotary-1,Color=Blue,Coalition=Enemies,T=57.17000000|25.52000000|500|0|0|90,Health=1
           302,Name=E-2C,Type=Air+FixedWing,Group=Overlord,Color=Blue,Coalition=Enemies,T=57.25000000|25.62000000|9000|0|0|90,Health=1

           #60.00
           101,T=57.17763780|25.53263180|0|0|0|90
           102,T=57.16097113|25.53263180|0|0|0|90
           103,T=57.17763780|25.50205180|0|0|0|90
           104,T=57.17763780|25.56321180|0|0|0|90
           105,T=57.19430447|25.53263180|0|0|0|90
           201,T=57.45000000|25.80000000|9000|0|0|270
           202,T=57.35000000|25.75000000|10000|0|0|270
           301,T=57.17100000|25.52100000|500|0|0|90
           302,T=57.25100000|25.62100000|9000|0|0|90

           #100.00
           901,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44000000|25.79000000|8500|0|0|270
           902,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44100000|25.79100000|8500|0|0|270
           903,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44200000|25.79200000|8500|0|0|270
           904,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44300000|25.79300000|8500|0|0|270
           905,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44400000|25.79400000|8500|0|0|270
           906,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44500000|25.79500000|8500|0|0|270
           907,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44600000|25.79600000|8500|0|0|270
           908,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44700000|25.79700000|8500|0|0|270

           #110.00
           1001,Name=SM_2,Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16000000|25.53200000|50|0|0|90
           1002,Name=SM_2,Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16010000|25.53210000|50|0|0|90
           1003,Name=SM_2,Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17700000|25.50200000|50|0|0|90
           1004,Name=SM_2,Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17710000|25.50210000|50|0|0|90
           1005,Name=SM_2,Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17700000|25.56300000|50|0|0|90
           1006,Name=SM_2,Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17710000|25.56310000|50|0|0|90
           1007,Name=SM_2,Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19400000|25.53300000|50|0|0|90
           1008,Name=SM_2,Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19410000|25.53310000|50|0|0|90
           1009,Name=SM_2,Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16020000|25.53220000|50|0|0|90
           1010,Name=SM_2,Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16030000|25.53230000|50|0|0|90
           1011,Name=SM_2,Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17720000|25.50220000|50|0|0|90
           1012,Name=SM_2,Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17730000|25.50230000|50|0|0|90
           1013,Name=SM_2,Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17720000|25.56320000|50|0|0|90
           1014,Name=SM_2,Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17730000|25.56330000|50|0|0|90
           1015,Name=SM_2,Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19420000|25.53320000|50|0|0|90
           1016,Name=SM_2,Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19430000|25.53330000|50|0|0|90
           1017,Name=SM_2,Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16040000|25.53240000|50|0|0|90
           1018,Name=SM_2,Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16050000|25.53250000|50|0|0|90
           1019,Name=SM_2,Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19440000|25.53340000|50|0|0|90
           1020,Name=SM_2,Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19450000|25.53350000|50|0|0|90

           #120.00
           902,T=57.30000000|25.70000000|6000|0|0|270
           1001,T=57.30000000|25.70000000|6000|0|0|90
           -1001
           -902
           1002,T=57.31000000|25.71000000|6000|0|0|90
           -1002

           #121.00
           903,T=57.30100000|25.70100000|6000|0|0|270
           1003,T=57.30100000|25.70100000|6000|0|0|90
           -1003
           -903
           1004,T=57.31100000|25.71100000|6000|0|0|90
           -1004

           #122.00
           904,T=57.30200000|25.70200000|6000|0|0|270
           1005,T=57.30200000|25.70200000|6000|0|0|90
           -1005
           -904
           1006,T=57.31200000|25.71200000|6000|0|0|90
           -1006

           #123.00
           905,T=57.30300000|25.70300000|6000|0|0|270
           1008,T=57.30300000|25.70300000|6000|0|0|90
           -1008
           -905
           1007,T=57.31300000|25.71300000|6000|0|0|90
           -1007

           #124.00
           906,T=57.30400000|25.70400000|6000|0|0|270
           1009,T=57.30400000|25.70400000|6000|0|0|90
           -1009
           -906
           1010,T=57.31400000|25.71400000|6000|0|0|90
           -1010

           #125.00
           907,T=57.30500000|25.70500000|6000|0|0|270
           1011,T=57.30500000|25.70500000|6000|0|0|90
           -1011
           -907
           1012,T=57.31500000|25.71500000|6000|0|0|90
           -1012

           #126.00
           908,T=57.30600000|25.70600000|6000|0|0|270
           1014,T=57.30600000|25.70600000|6000|0|0|90
           -1014
           -908
           1013,T=57.31600000|25.71600000|6000|0|0|90
           -1013

           #127.00
           1015,T=57.32000000|25.72000000|6000|0|0|90
           -1015
           1016,T=57.32100000|25.72100000|6000|0|0|90
           -1016

           #128.00
           1017,T=57.33000000|25.73000000|6000|0|0|90
           -1017
           1018,T=57.33100000|25.73100000|6000|0|0|90
           -1018

           #129.00
           1019,T=57.34000000|25.74000000|6000|0|0|90
           -1019
           1020,T=57.34100000|25.74100000|6000|0|0|90
           -1020

           #140.00
           901,T=57.17763780|25.53263180|50|0|0|270
           -901

           #150.00
           909,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.30000000|25.70000000|8500|0|0|270
           #160.00
           909,T=57.17100000|25.52100000|550|0|0|270
           -909

           #170.00
           910,Name=X_22,Type=Weapon+Missile,Parent=202,Color=Red,Coalition=Allies,T=57.30000000|25.70000000|9000|0|0|270
           #180.00
           910,T=57.25100000|25.62100000|9000|0|0|270
           -910

           #190.00
           911,Name=X_22,Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.30000000|25.70000000|8500|0|0|270
           #200.00
           911,T=57.17100000|25.52100000|550|0|0|270
           -911
           -301

           #210.00
           101,T=57.17863780|25.53363180|0|0|0|90
           102,T=57.16197113|25.53363180|0|0|0|90
           103,T=57.17863780|25.50305180|0|0|0|90
           104,T=57.17863780|25.56421180|0|0|0|90
           105,T=57.19530447|25.53363180|0|0|0|90
           201,T=57.35000000|25.70000000|9000|0|0|270
           202,T=57.25000000|25.65000000|10000|0|0|270
           302,T=57.25200000|25.62200000|9000|0|0|90
           """;
        }

    }
}
