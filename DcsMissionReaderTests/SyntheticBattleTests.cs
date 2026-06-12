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

		private sealed record TestObjectIdentity(
			string Name,
			string Group,
			string Pilot)
		{
			public string DisplayName => $"{Group}-{Pilot}";
		}

		private static readonly TestObjectIdentity Carrier = new(
			Name: "CVN_73",
			Group: "Washington CSG",
			Pilot: "Washington");

		private static readonly TestObjectIdentity DdgAstern = new(
			Name: "USS Truxtun DDG-103",
			Group: "DDG Astern",
			Pilot: "Truxtun");

		private static readonly TestObjectIdentity DdgPort = new(
			Name: "USS Gridley DDG-101",
			Group: "DDG Port",
			Pilot: "Gridley");

		private static readonly TestObjectIdentity DdgStarboard = new(
			Name: "USS Stockdale DDG-106",
			Group: "DDG Starboard",
			Pilot: "Stockdale");

		private static readonly TestObjectIdentity CgAhead = new(
			Name: "USS Vicksburg CG-69",
			Group: "CG Ahead / AAW Picket",
			Pilot: "Vicksburg");

		private static readonly TestObjectIdentity CarrierKiller = new(
			Name: "Tu-22M3",
			Group: "Carrier Killer Group",
			Pilot: "Pyetr");

		private static readonly TestObjectIdentity AwacsKiller = new(
			Name: "MiG-31",
			Group: "AWACS Killer Group",
			Pilot: "Ivan");

		private static readonly TestObjectIdentity Rotary = new(
			Name: "SH-60B",
			Group: "Rotary-1",
			Pilot: "Max");

		private static readonly TestObjectIdentity Overlord = new(
			Name: "E-2C",
			Group: "Overlord",
			Pilot: "Hollywood");

		private const string KitchenWeaponName = "X_22";

		private const string Sm2WeaponName = "SM_2";

		private const string Sm2ErWeaponName = "SM_2ER";

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
						EnableTerminalProximityDamageInference = false,
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

				Assert.Contains(Carrier.Group, kml);
				Assert.Contains(DdgAstern.Name, kml);
				Assert.Contains(DdgPort.Name, kml);
				Assert.Contains(DdgStarboard.Name, kml);
				Assert.Contains(CgAhead.Name, kml);

				Assert.Contains(CarrierKiller.Group, kml);
				Assert.Contains(AwacsKiller.Group, kml);
				Assert.Contains(Rotary.Group, kml);
				Assert.Contains(Overlord.Group, kml);

				Assert.Contains(KitchenWeaponName, kml);
				Assert.Contains(Sm2WeaponName, kml);

				/*
                File.WriteAllText(
    Path.Combine(tempDirectory, "synthetic-csg-escort-battle.doc.kml"),
    kml);
                */

				AssertFolderNameContainsAll(kml, Sm2WeaponName, DdgAstern.DisplayName);
				AssertFolderNameContainsAll(kml, Sm2WeaponName, DdgPort.DisplayName);
				AssertFolderNameContainsAll(kml, Sm2WeaponName, DdgStarboard.DisplayName);
				AssertFolderNameContainsAll(kml, Sm2WeaponName, CgAhead.DisplayName);
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
						EnableTerminalProximityDamageInference = false,
						EnableTerminalProximityNearMissReporting = true
					});

				service.CreatePostBriefingKml(zipPath, outputPath);

				string kml = ReadKmlFromKmz(outputPath);

				Assert.Equal(
					20,
					CountFolderNamesContainingAll(kml, Sm2WeaponName));

				Assert.Equal(
					6,
					CountFolderNamesContainingAll(kml, Sm2WeaponName, DdgAstern.DisplayName));

				Assert.Equal(
					4,
					CountFolderNamesContainingAll(kml, Sm2WeaponName, DdgPort.DisplayName));

				Assert.Equal(
					4,
					CountFolderNamesContainingAll(kml, Sm2WeaponName, DdgStarboard.DisplayName));

				Assert.Equal(
					6,
					CountFolderNamesContainingAll(kml, Sm2WeaponName, CgAhead.DisplayName));
			}
			finally
			{
				Directory.Delete(tempDirectory, recursive: true);
			}
		}

		[Fact]
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
						EnableTerminalProximityDamageInference = false,
						EnableTerminalProximityNearMissReporting = true
					});

				service.CreatePostBriefingKml(zipPath, outputPath);

				string kml = ReadKmlFromKmz(outputPath);

				Assert.Equal(
					20,
					CountFolderNamesContainingAll(kml, Sm2WeaponName));

				Assert.Equal(
					7,
					CountPlacemarkNameOccurrences(kml, $"Destroyed - {KitchenWeaponName}"));

				Assert.Equal(
					13,
					CountPlacemarkNameOccurrences(kml, $"Timeout - {Sm2WeaponName}"));
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
						EnableTerminalProximityDamageInference = false,
						EnableTerminalProximityNearMissReporting = true
					});

				PostBriefingKmlResult result = service.CreatePostBriefingKml(zipPath, outputPath);

				string kml = ReadKmlFromKmz(outputPath);
				File.WriteAllText(kmlDumpPath, kml);

				output.WriteLine($"KMZ: {outputPath}");
				output.WriteLine($"KML: {kmlDumpPath}");
				output.WriteLine($"GroupTrackCount: {result.GroupTrackCount}");
				output.WriteLine($"WeaponEmploymentCount: {result.WeaponEmploymentCount}");
				output.WriteLine($"SM_2 literal count: {CountLiteralOccurrences(kml, Sm2WeaponName)}");
				output.WriteLine($"X_22 literal count: {CountLiteralOccurrences(kml, KitchenWeaponName)}");
				output.WriteLine($"Destroyed - X_22 count: {CountPlacemarkNameOccurrences(kml, "Destroyed - " + KitchenWeaponName)}");
				output.WriteLine($"Timeout - SM_2 count: {CountPlacemarkNameOccurrences(kml, "Timeout - " + Sm2WeaponName)}");
				output.WriteLine($"Near Miss - Rotary-1 count: {CountPlacemarkNameOccurrences(kml, "Near Miss - " + Rotary.DisplayName)}");
				output.WriteLine($"Near Miss - Overlord count: {CountPlacemarkNameOccurrences(kml, "Near Miss - " + Overlord.DisplayName)}");

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

				Assert.Contains(Carrier.Group, kml);
				Assert.Contains("DDG Astern", kml);
				Assert.Contains("DDG Port", kml);
				Assert.Contains("DDG Starboard", kml);
				Assert.Contains("CG Ahead", kml);
				Assert.Contains(Overlord.Group, kml);
				Assert.Contains("SAR", kml);
				Assert.Contains("Carrier Killer", kml);
				Assert.Contains("AWACS KILLER", kml);

				Assert.Contains(KitchenWeaponName, kml);
				Assert.Contains("SM_2ER", kml);

				// The player sat on the Washington and the DDG/CG screen protected the CSG.
				string realCarrierDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: Washington CSG");

				string realDdgAsternDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: DDG Astern");

				string realDdgPortDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: DDG Port");

				string realDdgStarboardDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: DDG Starboard");

				string realCgAheadDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: CG Ahead");

				string realOverlordDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: Overlord");

				string realSarDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: SAR");

				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: realCarrierDisplayName,
					expectedHitCount: 0,
					expectedWeaponName: null);

				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: realDdgAsternDisplayName,
					expectedHitCount: 0,
					expectedWeaponName: null);

				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: realDdgPortDisplayName,
					expectedHitCount: 0,
					expectedWeaponName: null);

				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: realDdgStarboardDisplayName,
					expectedHitCount: 0,
					expectedWeaponName: null);

				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: realCgAheadDisplayName,
					expectedHitCount: 0,
					expectedWeaponName: null);

				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: realOverlordDisplayName,
					expectedHitCount: 0,
					expectedWeaponName: null);

				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: realSarDisplayName,
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

				string realAwacsKillerDisplayName = FindObjectDispositionPlacemarkNameByDescriptionText(
					kml,
					"Group: AWACS KILLER");

				Assert.True(
					CountPlacemarkNameOccurrences(kml, $"Destroyed - {realAwacsKillerDisplayName}") >= 1,
					$"Expected at least one destroyed {realAwacsKillerDisplayName} result.");
			}
			finally
			{
				Directory.Delete(tempDirectory, recursive: true);
			}
		}

		[Fact]
		public void CreatePostBriefingKml_WithSyntheticCsgEscortBattle_TerminalProximityOnlyDoesNotCreateObjectDamage()
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
						EnableTerminalProximityDamageInference = false,
						EnableTerminalProximityNearMissReporting = true
					});

				service.CreatePostBriefingKml(zipPath, outputPath);

				string kml = ReadKmlFromKmz(outputPath);

				// X_22 901 ends near Washington CSG, but terminal proximity alone is diagnostic only.
				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: Carrier.DisplayName,
					expectedHitCount: 0,
					expectedWeaponName: KitchenWeaponName);

				// X_22 909 ends near Rotary-1, but terminal proximity alone should not damage air targets.
				// Rotary-1 should only have the one real same-time-removal kill from X_22 911.
				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: Rotary.DisplayName,
					expectedHitCount: 1,
					expectedWeaponName: KitchenWeaponName);

				// X_22 910 ends near Overlord, but terminal proximity alone should not damage air targets.
				AssertObjectWeaponHitCount(
					kml,
					objectPlacemarkName: Overlord.DisplayName,
					expectedHitCount: 0,
					expectedWeaponName: KitchenWeaponName);

				Assert.Equal(
					1,
					CountPlacemarkNameOccurrences(kml, $"Near Miss - {Carrier.DisplayName}"));

				Assert.Equal(
					1,
					CountPlacemarkNameOccurrences(kml, $"Near Miss - {Rotary.DisplayName}"));

				Assert.Equal(
					1,
					CountPlacemarkNameOccurrences(kml, $"Near Miss - {Overlord.DisplayName}"));
			}
			finally
			{
				Directory.Delete(tempDirectory, recursive: true);
			}
		}

		[Fact]
		public void CreatePostBriefingKml_WithSyntheticCsgEscortBattle_HidesNonEffectWeaponFoldersByDefault()
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
						EnableTerminalProximityDamageInference = false,
						EnableTerminalProximityNearMissReporting = true
					});

				service.CreatePostBriefingKml(zipPath, outputPath);

				string kml = ReadKmlFromKmz(outputPath);

				Assert.Equal(
					20,
					CountFolderNamesContainingAll(kml, Sm2WeaponName));

				// 7 SM_2s intercept X_22s and should be active.
				Assert.Equal(
					7,
					CountFolderNamesContainingAllWithVisibility(kml, expectedVisibility: "1", Sm2WeaponName));

				// 13 SM_2s do not produce an effect and should be hidden.
				Assert.Equal(
					13,
					CountFolderNamesContainingAllWithVisibility(kml, expectedVisibility: "0", Sm2WeaponName));

				// Result markers stay hidden on the map even when their parent folder is active.
				AssertPlacemarkVisibility(kml, $"Destroyed - {KitchenWeaponName}", expectedVisibility: "0");
				AssertPlacemarkVisibility(kml, $"Timeout - {Sm2WeaponName}", expectedVisibility: "0");
				AssertPlacemarkVisibility(kml, $"Near Miss - {Carrier.DisplayName}", expectedVisibility: "0");
				AssertPlacemarkVisibility(kml, $"Near Miss - {Rotary.DisplayName}", expectedVisibility: "0");
			}
			finally
			{
				Directory.Delete(tempDirectory, recursive: true);
			}
		}

		private static int CountFolderNamesContainingAllWithVisibility(
	string kml,
	string expectedVisibility,
	params string[] expectedParts)
		{
			string normalizedKml = NormalizeLineEndings(kml);

			const string folderStartTag = "<Folder>";
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

				int nameStartIndex = normalizedKml.IndexOf(nameStartTag, folderStartIndex, StringComparison.Ordinal);

				if (nameStartIndex < 0)
				{
					break;
				}

				int nameEndIndex = normalizedKml.IndexOf(nameEndTag, nameStartIndex, StringComparison.Ordinal);

				if (nameEndIndex < 0)
				{
					break;
				}

				string folderName = normalizedKml[
					(nameStartIndex + nameStartTag.Length)..nameEndIndex];

				bool nameMatches = expectedParts.All(part =>
					folderName.Contains(part, StringComparison.OrdinalIgnoreCase));

				if (nameMatches)
				{
					int headerEndIndex = normalizedKml.IndexOf("<Folder>", nameEndIndex, StringComparison.Ordinal);

					if (headerEndIndex < 0)
					{
						headerEndIndex = Math.Min(normalizedKml.Length, nameEndIndex + 500);
					}

					string folderHeader = normalizedKml[folderStartIndex..headerEndIndex];

					bool visibilityMatches = folderHeader.Contains(
						$"<visibility>{expectedVisibility}</visibility>",
						StringComparison.Ordinal);

					if (visibilityMatches)
					{
						count++;
					}
				}

				searchIndex = nameEndIndex + nameEndTag.Length;
			}

			return count;
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

		private static string FindObjectDispositionPlacemarkNameByDescriptionText(
			string kml,
			string requiredDescriptionText)
		{
			string normalizedKml = NormalizeLineEndings(kml);
			string normalizedRequiredText = NormalizeLineEndings(requiredDescriptionText);

			const string placemarkStart = "<Placemark>";
			const string placemarkEnd = "</Placemark>";

			int searchIndex = 0;

			while (true)
			{
				int placemarkStartIndex = normalizedKml.IndexOf(
					placemarkStart,
					searchIndex,
					StringComparison.Ordinal);

				if (placemarkStartIndex < 0)
				{
					break;
				}

				int placemarkEndIndex = normalizedKml.IndexOf(
					placemarkEnd,
					placemarkStartIndex,
					StringComparison.Ordinal);

				if (placemarkEndIndex < 0)
				{
					break;
				}

				string placemark = normalizedKml[
					placemarkStartIndex..(placemarkEndIndex + placemarkEnd.Length)];

				if (placemark.Contains(
						"Weapons That Hit / Destroyed This Object:",
						StringComparison.Ordinal)
					&& placemark.Contains(
						normalizedRequiredText,
						StringComparison.Ordinal))
				{
					return ExtractPlacemarkName(placemark);
				}

				searchIndex = placemarkEndIndex + placemarkEnd.Length;
			}

			Assert.Fail(
				$"Could not find object disposition placemark containing description text: {requiredDescriptionText}");

			return string.Empty;
		}

		private static string ExtractPlacemarkName(string placemark)
		{
			const string nameStartTag = "<name>";
			const string nameEndTag = "</name>";

			int nameStartIndex = placemark.IndexOf(nameStartTag, StringComparison.Ordinal);
			int nameEndIndex = placemark.IndexOf(nameEndTag, StringComparison.Ordinal);

			Assert.True(
				nameStartIndex >= 0 && nameEndIndex > nameStartIndex,
				$"Could not extract name from placemark:{Environment.NewLine}{placemark}");

			string encodedName = placemark[
				(nameStartIndex + nameStartTag.Length)..nameEndIndex];

			return SecurityElement.FromString($"<root>{encodedName}</root>")?.Text
				?? encodedName;
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
			return $$"""
           FileType=text/acmi/tacview
           FileVersion=2.2
           0,ReferenceTime=2016-06-21T04:30:00Z

           #0.00
           101,Name={{Carrier.Name}},Type=Sea+Watercraft+AircraftCarrier,Group={{Carrier.Group}},Pilot={{Carrier.Pilot}},Color=Blue,Coalition=Enemies,T=57.17663780|25.53163180|0|0|0|90,Health=1
           102,Name={{DdgAstern.Name}},Type=Sea+Watercraft+Destroyer,Group={{DdgAstern.Group}},Pilot={{DdgAstern.Pilot}},Color=Blue,Coalition=Enemies,T=57.15997113|25.53163180|0|0|0|90,Health=1
           103,Name={{DdgPort.Name}},Type=Sea+Watercraft+Destroyer,Group={{DdgPort.Group}},Pilot={{DdgPort.Pilot}},Color=Blue,Coalition=Enemies,T=57.17663780|25.50105180|0|0|0|90,Health=1
           104,Name={{DdgStarboard.Name}},Type=Sea+Watercraft+Destroyer,Group={{DdgStarboard.Group}},Pilot={{DdgStarboard.Pilot}},Color=Blue,Coalition=Enemies,T=57.17663780|25.56221180|0|0|0|90,Health=1
           105,Name={{CgAhead.Name}},Type=Sea+Watercraft+Cruiser,Group={{CgAhead.Group}},Pilot={{CgAhead.Pilot}},Color=Blue,Coalition=Enemies,T=57.19330447|25.53163180|0|0|0|90,Health=1

           201,Name={{CarrierKiller.Name}},Type=Air+FixedWing,Group={{CarrierKiller.Group}},Pilot={{CarrierKiller.Pilot}},Color=Red,Coalition=Allies,T=57.55000000|25.90000000|9000|0|0|270,Health=1
           202,Name={{AwacsKiller.Name}},Type=Air+FixedWing,Group={{AwacsKiller.Group}},Pilot={{AwacsKiller.Pilot}},Color=Red,Coalition=Allies,T=57.45000000|25.85000000|10000|0|0|270,Health=1

           301,Name={{Rotary.Name}},Type=Air+Rotorcraft,Group={{Rotary.Group}},Pilot={{Rotary.Pilot}},Color=Blue,Coalition=Enemies,T=57.17000000|25.52000000|500|0|0|90,Health=1
           302,Name={{Overlord.Name}},Type=Air+FixedWing,Group={{Overlord.Group}},Pilot={{Overlord.Pilot}},Color=Blue,Coalition=Enemies,T=57.25000000|25.62000000|9000|0|0|90,Health=1

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
           901,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44000000|25.79000000|8500|0|0|270
           902,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44100000|25.79100000|8500|0|0|270
           903,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44200000|25.79200000|8500|0|0|270
           904,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44300000|25.79300000|8500|0|0|270
           905,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44400000|25.79400000|8500|0|0|270
           906,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44500000|25.79500000|8500|0|0|270
           907,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44600000|25.79600000|8500|0|0|270
           908,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.44700000|25.79700000|8500|0|0|270

           #110.00
           1001,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16000000|25.53200000|50|0|0|90
           1002,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16010000|25.53210000|50|0|0|90
           1003,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17700000|25.50200000|50|0|0|90
           1004,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17710000|25.50210000|50|0|0|90
           1005,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17700000|25.56300000|50|0|0|90
           1006,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17710000|25.56310000|50|0|0|90
           1007,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19400000|25.53300000|50|0|0|90
           1008,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19410000|25.53310000|50|0|0|90
           1009,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16020000|25.53220000|50|0|0|90
           1010,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16030000|25.53230000|50|0|0|90
           1011,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17720000|25.50220000|50|0|0|90
           1012,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=103,Color=Blue,Coalition=Enemies,T=57.17730000|25.50230000|50|0|0|90
           1013,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17720000|25.56320000|50|0|0|90
           1014,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=104,Color=Blue,Coalition=Enemies,T=57.17730000|25.56330000|50|0|0|90
           1015,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19420000|25.53320000|50|0|0|90
           1016,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19430000|25.53330000|50|0|0|90
           1017,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16040000|25.53240000|50|0|0|90
           1018,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=102,Color=Blue,Coalition=Enemies,T=57.16050000|25.53250000|50|0|0|90
           1019,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19440000|25.53340000|50|0|0|90
           1020,Name={{Sm2WeaponName}},Type=Weapon+Missile,Parent=105,Color=Blue,Coalition=Enemies,T=57.19450000|25.53350000|50|0|0|90

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
           101,T=57.17763780|25.53263180|0|0|0|90
           901,T=57.17763780|25.53263180|50|0|0|270
           -901

           #150.00
           909,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.30000000|25.70000000|8500|0|0|270
           #160.00
           301,T=57.17100000|25.52100000|500|0|0|90
           909,T=57.17100000|25.52100000|550|0|0|270
           -909

           #170.00
           910,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=202,Color=Red,Coalition=Allies,T=57.30000000|25.70000000|9000|0|0|270
           #180.00
           302,T=57.25100000|25.62100000|9000|0|0|90
           910,T=57.25100000|25.62100000|9000|0|0|270
           -910

           #190.00
           911,Name={{KitchenWeaponName}},Type=Weapon+Missile,Parent=201,Color=Red,Coalition=Allies,T=57.30000000|25.70000000|8500|0|0|270
           #200.00
           301,T=57.17100000|25.52100000|500|0|0|90
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
