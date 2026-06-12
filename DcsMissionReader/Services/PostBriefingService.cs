using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DcsMissionReader.Services
{
    public sealed class PostBriefingService : IPostBriefingService
    {
        private readonly IBriefingStylesService _briefingStylesService;

        public PostBriefingService(
            IBriefingStylesService? briefingStylesService = null)
        {
            _briefingStylesService = briefingStylesService ?? new BriefingStylesService();
        }
        public PostBriefingKmlResult CreatePostBriefingKml(
        string acmiZipFilePath,
        string? outputKmlFilePath = null,
        PostBriefingKmlOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(acmiZipFilePath))
            {
                throw new ArgumentException("ACMI zip file path is required.", nameof(acmiZipFilePath));
            }

            if (!File.Exists(acmiZipFilePath))
            {
                throw new FileNotFoundException("ACMI zip file was not found.", acmiZipFilePath);
            }

            options ??= new PostBriefingKmlOptions();

            outputKmlFilePath = EnsureKmzOutputPath(
                outputKmlFilePath ?? CreateDefaultOutputPath(acmiZipFilePath));

            TacviewAcmiParseData acmiData = TacviewAcmiParser.ParseZippedAcmi(acmiZipFilePath);

            TacviewCombatReport lifecycleCombatReport =
                TacviewLifecycleCombatReportService.BuildFromZippedAcmiFile(acmiZipFilePath);

            AcmiParseResult parseResult = BuildPostBriefingParseResult(
                acmiData,
                lifecycleCombatReport);

            string kml = BuildKml(parseResult, options);

            WritePostBriefingOutput(outputKmlFilePath, kml);

            return new PostBriefingKmlResult
            {
                SourceAcmiZipFilePath = acmiZipFilePath,
                OutputKmlFilePath = outputKmlFilePath,
                GroupTrackCount = parseResult.GroupTracks.Count,
                WeaponEmploymentCount = parseResult.WeaponEngagements.Count,
                WeaponResultCount =
                    parseResult.WeaponEngagements.Sum(e => e.Results.Count)
                    + parseResult.UnmatchedWeaponResults.Count
            };
        }
        private AcmiParseResult BuildPostBriefingParseResult(
            TacviewAcmiParseData acmiData,
            TacviewCombatReport lifecycleCombatReport)
        {
            List<TacviewObjectTrack> groupTracks = acmiData.Objects.Values
                .Where(o => !ShouldSuppressFromObjectTracks(o))
                .Where(o => o.Samples.Count > 0)
                .OrderBy(o => o.Group ?? o.Name ?? o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<TacviewWeaponEmployment> oldWeaponEmployments = acmiData.Objects.Values
                .Where(o => o.IsWeapon)
                .Where(IsRelevantWeaponObject)
                .Where(o => o.Start is not null)
                .Select(o => PostBriefingWeaponEmploymentFactory.CreateWeaponEmployment(o, acmiData.Objects))
                .ToList();

            List<TacviewWeaponResult> oldWeaponResults = acmiData.Events
                .Where(PostBriefingWeaponEventResultFactory.IsWeaponResultEventType)
                .Select(e => PostBriefingWeaponEventResultFactory.CreateWeaponResult(e, acmiData.Objects))
                .ToList();

            List<TacviewWeaponEmployment> lifecycleWeaponEmployments =
                CreateWeaponEmploymentsFromLifecycleReport(
                    lifecycleCombatReport,
                    acmiData.Objects);

            List<TacviewWeaponResult> lifecycleWeaponResults =
                CreateWeaponResultsFromLifecycleReport(
                    lifecycleCombatReport,
                    acmiData.Objects,
                    acmiData.ReferenceTimeUtc);

            List<TacviewWeaponEmployment> weaponEmployments =
                MergeWeaponEmployments(
                    oldWeaponEmployments,
                    lifecycleWeaponEmployments,
                    oldWeaponResults,
                    lifecycleWeaponResults);

            List<TacviewWeaponResult> weaponResults =
                MergeWeaponResults(
                    oldWeaponResults,
                    lifecycleWeaponResults); List<TacviewObjectTrack> weaponTracks = acmiData.Objects.Values
                .Where(IsRelevantWeaponObject)
                .Where(o => o.Samples.Count > 0)
                .OrderBy(o => o.Name ?? o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<TacViewWeaponEngagement> weaponEngagements = CreateWeaponEngagements(
                weaponTracks,
                weaponEmployments,
                weaponResults,
                out List<TacviewWeaponResult> unmatchedWeaponResults);

            return new AcmiParseResult(
                acmiData.Mission,
                groupTracks,
                weaponEngagements,
                unmatchedWeaponResults,
                acmiData.ReferenceTimeUtc);
        }

        private static string CreateDefaultOutputPath(string acmiZipFilePath)
        {
            string directory = Path.GetDirectoryName(acmiZipFilePath) ?? Environment.CurrentDirectory;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(acmiZipFilePath);

            if (fileNameWithoutExtension.EndsWith(".acmi", StringComparison.OrdinalIgnoreCase))
            {
                fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithoutExtension);
            }

            return Path.Combine(directory, $"{fileNameWithoutExtension}.postbrief.kmz");
        }

        private static string EnsureKmzOutputPath(string outputPath)
        {
            string directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputPath);

            if (outputPath.EndsWith(".postbrief.kml", StringComparison.OrdinalIgnoreCase))
            {
                fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithoutExtension);
            }

            return Path.Combine(directory, $"{fileNameWithoutExtension}.kmz");
        }

        private static void WritePostBriefingOutput(
            string outputFilePath,
            string kml)
        {
            string kmzOutputPath = EnsureKmzOutputPath(outputFilePath);

            string? outputDirectory = Path.GetDirectoryName(kmzOutputPath);

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            WriteKmz(kmzOutputPath, kml);
        }

        private static void WriteKmz(
            string outputKmzFilePath,
            string kml)
        {
            if (File.Exists(outputKmzFilePath))
            {
                File.Delete(outputKmzFilePath);
            }

            using ZipArchive archive = ZipFile.Open(outputKmzFilePath, ZipArchiveMode.Create);

            ZipArchiveEntry kmlEntry = archive.CreateEntry("doc.kml", CompressionLevel.Optimal);

            using (Stream stream = kmlEntry.Open())
            using (StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(kml);
            }

            AddIconToKmzIfAvailable(archive, "missile.png");
            AddIconToKmzIfAvailable(archive, "bomb.png");
            AddIconToKmzIfAvailable(archive, "sam.png");
            AddIconToKmzIfAvailable(archive, "explode.png");
        }

        private static void AddIconToKmzIfAvailable(
            ZipArchive archive,
            string iconFileName)
        {
            string? iconPath = FindKmlIconPath(iconFileName);

            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            {
                return;
            }

            archive.CreateEntryFromFile(
                iconPath,
                $"icons/{iconFileName}",
                CompressionLevel.Optimal);
        }

        private static string? FindKmlIconPath(string iconFileName)
        {
            string[] candidatePaths =
            [
                Path.Combine(AppContext.BaseDirectory, "Data", "KmlIcons", iconFileName),
                Path.Combine(Environment.CurrentDirectory, "Data", "KmlIcons", iconFileName),
                Path.Combine(AppContext.BaseDirectory, "KmlIcons", iconFileName),
                Path.Combine(Environment.CurrentDirectory, "KmlIcons", iconFileName)
            ];

            return candidatePaths.FirstOrDefault(File.Exists);
        }
        private static bool ShouldShowWeaponEngagementByDefault(TacViewWeaponEngagement engagement)
        {
            return engagement.Results.Any(result => IsDefaultVisibleWeaponResult(result));
        }

        private static bool IsDefaultVisibleWeaponResult(TacviewWeaponResult result)
        {
            if (IsObjectEffectWeaponResultType(result.EventType))
            {
                return true;
            }

            return result.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(result.TargetObjectId)
                && !string.IsNullOrWhiteSpace(result.TargetName);
        }
        private static bool IsObjectEffectWeaponResultType(string eventType)
        {
            return eventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFailedOrDiagnosticWeaponResultType(string eventType)
        {
            return eventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsSuppressedResultObject(TacviewObjectTrack track)
        {
            string combined = $"{track.Name} {track.Type} {track.Group}";

            return IsCountermeasureOrDecoy(combined)
                || IsJettisonedStore(combined);
        }

        private bool ShouldSuppressFromObjectTracks(TacviewObjectTrack track)
        {
            if (track.IsWeapon)
            {
                return true;
            }

            string combined = $"{track.Name} {track.Type} {track.Group}".ToLowerInvariant();

            return IsCountermeasureOrDecoy(combined)
                || IsJettisonedStore(combined);
        }

        private static bool IsCountermeasureOrDecoy(string value)
        {
            return value.Contains("chaff", StringComparison.OrdinalIgnoreCase)
                || value.Contains("flare", StringComparison.OrdinalIgnoreCase)
                || value.Contains("decoy", StringComparison.OrdinalIgnoreCase)
                || value.Contains("countermeasure", StringComparison.OrdinalIgnoreCase)
                || value.Contains("pilot", StringComparison.OrdinalIgnoreCase)
                || value.Contains("misc+shrapnel", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsJettisonedStore(string value)
        {
            return value.Contains("fuel tank", StringComparison.OrdinalIgnoreCase)
                || value.Contains("fueltank", StringComparison.OrdinalIgnoreCase)
                || value.Contains("drop tank", StringComparison.OrdinalIgnoreCase)
                || value.Contains("droptank", StringComparison.OrdinalIgnoreCase)
                || value.Contains("external tank", StringComparison.OrdinalIgnoreCase)
                || value.Contains("jettison", StringComparison.OrdinalIgnoreCase)
                || value.Contains("tank 300", StringComparison.OrdinalIgnoreCase)
                || value.Contains("tank 370", StringComparison.OrdinalIgnoreCase)
                || value.Contains("tank 600", StringComparison.OrdinalIgnoreCase)
                || value.Contains("tank 800", StringComparison.OrdinalIgnoreCase)
                || value.Contains("tank 1100", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGunRoundOrShell(string value)
        {
            return value.Contains("Projectile+Shell", StringComparison.OrdinalIgnoreCase)
                || value.Contains("weapons.shells", StringComparison.OrdinalIgnoreCase)
                || value.Contains("shell", StringComparison.OrdinalIgnoreCase)
                || value.Contains("bullet", StringComparison.OrdinalIgnoreCase)
                || value.Contains("cannon", StringComparison.OrdinalIgnoreCase)
                || value.Contains("gun round", StringComparison.OrdinalIgnoreCase)
                || value.Contains("M61", StringComparison.OrdinalIgnoreCase)
                || value.Contains("GAU-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("20_HE", StringComparison.OrdinalIgnoreCase)
                || value.Contains("20_AP", StringComparison.OrdinalIgnoreCase)
                || value.Contains("30_HE", StringComparison.OrdinalIgnoreCase)
                || value.Contains("30_AP", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRelevantWeaponObject(TacviewObjectTrack track)
        {
            if (!track.IsWeapon)
            {
                return false;
            }

            string combined = $"{track.Name} {track.Type} {track.Group}";

            return !IsCountermeasureOrDecoy(combined)
                && !IsJettisonedStore(combined)
                && !IsGunRoundOrShell(combined);
        }
        private static TacviewObjectTrack? ResolveWeaponShooter(
            TacviewObjectTrack weapon,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            if (string.IsNullOrWhiteSpace(weapon.ParentObjectId))
            {
                return null;
            }

            string parentId = weapon.ParentObjectId.Trim();

            if (objects.TryGetValue(parentId, out TacviewObjectTrack? directParent))
            {
                return directParent;
            }

            string normalizedParentId = NormalizeTacviewObjectId(parentId);

            return objects
                .FirstOrDefault(pair =>
                    NormalizeTacviewObjectId(pair.Key)
                        .Equals(normalizedParentId, StringComparison.OrdinalIgnoreCase))
                .Value;
        }

        private static string NormalizeTacviewObjectId(string value)
        {
            return value
                .Trim()
                .TrimStart('#')
                .Trim('{', '}')
                .Trim();
        }
        private static TacviewPositionSample? FindSampleClosestToTime(
            IReadOnlyList<TacviewPositionSample> samples,
            double timeSeconds)
        {
            if (samples.Count == 0)
            {
                return null;
            }

            TacviewPositionSample bestSample = samples[0];
            double bestDifference = Math.Abs(bestSample.TimeSeconds - timeSeconds);

            for (int i = 1; i < samples.Count; i++)
            {
                double difference = Math.Abs(samples[i].TimeSeconds - timeSeconds);

                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestSample = samples[i];
                }
            }

            return bestSample;
        }
         
        private string BuildKml(
            AcmiParseResult parseResult,
            PostBriefingKmlOptions options)
        {
            StringBuilder builder = new();

            builder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
            builder.AppendLine("""<kml xmlns="http://www.opengis.net/kml/2.2">""");
            builder.AppendLine("<Document>");
            builder.AppendLine("<name>DCS Tacview Post Brief</name>");

            _briefingStylesService.AppendStyles(builder);

            Dictionary<string, ObjectDisposition> dispositionsByObjectId =
                BuildObjectDispositionIndex(
                    parseResult.WeaponEngagements,
                    parseResult.UnmatchedWeaponResults);

            AppendMissionFolder(builder, parseResult.Mission);
            AppendGroupTracksFolder(builder, parseResult.GroupTracks, options, dispositionsByObjectId);
            AppendDestroyedObjectsFolder(builder, parseResult.GroupTracks, dispositionsByObjectId);

            AppendWeaponsFolder(
                builder,
                parseResult.WeaponEngagements,
                parseResult.UnmatchedWeaponResults,
                options);

            builder.AppendLine("</Document>");
            builder.AppendLine("</kml>");

            return builder.ToString();
        }

        private static Dictionary<string, ObjectDisposition> BuildObjectDispositionIndex(
            IReadOnlyList<TacViewWeaponEngagement> weaponEngagements,
            IReadOnlyList<TacviewWeaponResult> unmatchedWeaponResults)
        {
            Dictionary<string, List<ObjectWeaponHit>> hitsByObjectId = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TacviewWeaponResult> destroyedResultsByObjectId = new(StringComparer.OrdinalIgnoreCase);

            foreach (TacViewWeaponEngagement engagement in weaponEngagements)
            {
                string weaponName = string.IsNullOrWhiteSpace(engagement.Employment.WeaponName)
                    ? engagement.Employment.WeaponObjectId
                    : engagement.Employment.WeaponName;

                string shooterName =
                    engagement.Employment.ParentName
                    ?? engagement.Employment.ParentObjectId
                    ?? "Unknown";

                foreach (TacviewWeaponResult result in engagement.Results)
                {
                    AddObjectEffectResult(
                        result,
                        hitsByObjectId,
                        destroyedResultsByObjectId,
                        weaponName,
                        engagement.Employment.WeaponObjectId,
                        shooterName);
                }
            }

            foreach (TacviewWeaponResult result in unmatchedWeaponResults)
            {
                string weaponObjectId = !string.IsNullOrWhiteSpace(result.SourceObjectId)
                    ? result.SourceObjectId
                    : "Unknown Weapon";

                string weaponName = !string.IsNullOrWhiteSpace(result.SourceName)
                    ? result.SourceName
                    : weaponObjectId;

                string shooterName = !string.IsNullOrWhiteSpace(result.SourceName)
                    ? result.SourceName
                    : weaponObjectId;

                AddObjectEffectResult(
                    result,
                    hitsByObjectId,
                    destroyedResultsByObjectId,
                    weaponName,
                    weaponObjectId,
                    shooterName);
            }

            Dictionary<string, ObjectDisposition> dispositions = new(StringComparer.OrdinalIgnoreCase);

            foreach ((string objectId, List<ObjectWeaponHit> hits) in hitsByObjectId)
            {
                bool wasDestroyed = destroyedResultsByObjectId.TryGetValue(
                    objectId,
                    out TacviewWeaponResult? destroyedResult);

                dispositions[objectId] = new ObjectDisposition(
                    objectId,
                    wasDestroyed,
                    destroyedResult?.AbsoluteTimeUtc,
                    destroyedResult?.TimeSeconds ?? hits.Max(hit => hit.HitTimeSeconds),
                    hits
                        .OrderBy(hit => hit.HitTimeUtc)
                        .ThenBy(hit => hit.HitTimeSeconds)
                        .ThenBy(hit => hit.WeaponObjectId, StringComparer.OrdinalIgnoreCase)
                        .ToList());
            }

            return dispositions;
        }

        private static void AddObjectEffectResult(
            TacviewWeaponResult result,
            IDictionary<string, List<ObjectWeaponHit>> hitsByObjectId,
            IDictionary<string, TacviewWeaponResult> destroyedResultsByObjectId,
            string weaponName,
            string weaponObjectId,
            string shooterName)
        {
            if (!IsObjectEffectWeaponResultType(result.EventType))
            {
                return;
            }

            string? targetObjectId = result.TargetObjectId;

            if (string.IsNullOrWhiteSpace(targetObjectId))
            {
                return;
            }

            if (!hitsByObjectId.TryGetValue(targetObjectId, out List<ObjectWeaponHit>? hits))
            {
                hits = new List<ObjectWeaponHit>();
                hitsByObjectId[targetObjectId] = hits;
            }

            string outcome = string.IsNullOrWhiteSpace(result.EventType)
                ? result.Outcome ?? "Unknown"
                : result.EventType;

            bool alreadyRecorded = hits.Any(existingHit =>
                string.Equals(existingHit.WeaponObjectId, weaponObjectId, StringComparison.OrdinalIgnoreCase)
                && Nullable.Equals(existingHit.HitTimeSeconds, result.TimeSeconds)
                && Nullable.Equals(existingHit.HitTimeUtc, result.AbsoluteTimeUtc)
                && string.Equals(existingHit.Outcome, outcome, StringComparison.OrdinalIgnoreCase));

            if (alreadyRecorded)
            {
                return;
            }

            hits.Add(new ObjectWeaponHit(
                weaponName,
                weaponObjectId,
                shooterName,
                result.AbsoluteTimeUtc,
                result.TimeSeconds,
                outcome,
                result.Description ?? string.Empty));

            if (result.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase))
            {
                destroyedResultsByObjectId[targetObjectId] = result;
            }
        }

        private static void AppendMissionFolder(StringBuilder builder, TacviewMissionInfo mission)
        {
            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Mission</name>");

            string title = string.IsNullOrWhiteSpace(mission.Title)
                ? "Mission Details"
                : mission.Title;

            string description =
                $"Title: {mission.Title ?? "Unknown"}\n" +
                $"Category: {mission.Category ?? "Unknown"}\n" +
                $"Author: {mission.Author ?? "Unknown"}\n" +
                $"Data Source: {mission.DataSource ?? "Unknown"}\n" +
                $"Data Recorder: {mission.DataRecorder ?? "Unknown"}\n" +
                $"File Type: {mission.FileType ?? "Unknown"}\n" +
                $"File Version: {mission.FileVersion ?? "Unknown"}\n" +
                $"Reference Time: {(mission.ReferenceTimeUtc?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? "Unknown")}\n" +
                $"Recording Time: {(mission.RecordingTimeUtc?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? "Unknown")}\n" +
                $"Reference Latitude: {(mission.ReferenceLatitude?.ToString(CultureInfo.InvariantCulture) ?? "Unknown")}\n" +
                $"Reference Longitude: {(mission.ReferenceLongitude?.ToString(CultureInfo.InvariantCulture) ?? "Unknown")}\n\n" +
                $"Comments:\n{mission.Comments ?? "None"}\n\n" +
                $"Briefing:\n{mission.Briefing ?? "None"}";

            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", title);
            builder.AppendElement("description", description);

            if (mission.ReferenceTimeUtc is not null)
            {
                builder.AppendLine("<TimeStamp>");
                builder.AppendElement(
                    "when",
                    mission.ReferenceTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                builder.AppendLine("</TimeStamp>");
            }

            if (mission.ReferenceLongitude.HasValue && mission.ReferenceLatitude.HasValue)
            {
                builder.AppendLine("<Point>");
                builder.AppendElement(
                    "coordinates",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{mission.ReferenceLongitude.Value:F8},{mission.ReferenceLatitude.Value:F8},0"));
                builder.AppendLine("</Point>");
            }

            builder.AppendLine("</Placemark>");
            builder.AppendLine("</Folder>");
        }


        private static void AppendFolderStart(
            StringBuilder builder,
            string name,
            bool visible = true)
        {
            builder.AppendLine("<Folder>");
            builder.AppendElement("name", name);
            builder.AppendElement("visibility", visible ? "1" : "0");
        }

        private static void AppendGroupTracksFolder(
            StringBuilder builder,
            IReadOnlyList<TacviewObjectTrack> groupTracks,
            PostBriefingKmlOptions options,
            IReadOnlyDictionary<string, ObjectDisposition> dispositionsByObjectId)
        {
            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Object Tracks</name>");

            foreach (TacviewObjectTrack track in groupTracks)
            {
                if (track.Start is null)
                {
                    continue;
                }

                IReadOnlyList<TacviewPositionSample> sampledTrack = SelectEvenlyDistributedSamples(
                    track.Samples,
                    options.MaxTrackPointsPerObject);

                string displayName = GetDisplayName(track);

                builder.AppendLine("<Folder>");
                builder.AppendElement("name", displayName);

                if (IsBullseye(track))
                {
                    AppendBullseye(builder, track, options);
                    builder.AppendLine("</Folder>");
                    continue;
                }

                AppendStartObjectPlacemark(builder, track, options, dispositionsByObjectId);

                if (sampledTrack.Count >= 2)
                {
                    AppendLineStringPlacemark(
                        builder,
                        displayName,
                        BuildObjectDescription(track, options, dispositionsByObjectId),
                        sampledTrack,
                        GetTrackStyleUrl(track, options));
                }

                AppendTrackSamplePoints(builder, track, sampledTrack, options);

                builder.AppendLine("</Folder>");
            }

            builder.AppendLine("</Folder>");
        }

        private static IReadOnlyList<TacviewPositionSample> SelectEvenlyDistributedSamples(
            IReadOnlyList<TacviewPositionSample> samples,
            int maxPoints)
        {
            if (samples.Count == 0)
            {
                return Array.Empty<TacviewPositionSample>();
            }

            if (maxPoints <= 0 || samples.Count <= maxPoints)
            {
                return samples;
            }

            if (maxPoints == 1)
            {
                return new[] { samples[0] };
            }

            var selectedIndexes = new SortedSet<int>();
            double step = (samples.Count - 1) / (double)(maxPoints - 1);

            for (int i = 0; i < maxPoints; i++)
            {
                int index = (int)Math.Round(i * step, MidpointRounding.AwayFromZero);
                index = Math.Clamp(index, 0, samples.Count - 1);
                selectedIndexes.Add(index);
            }

            selectedIndexes.Add(0);
            selectedIndexes.Add(samples.Count - 1);

            return selectedIndexes
                .Take(maxPoints)
                .Select(index => samples[index])
                .ToList();
        }

        private static void AppendStartObjectPlacemark(
            StringBuilder builder,
            TacviewObjectTrack track,
            PostBriefingKmlOptions options,
            IReadOnlyDictionary<string, ObjectDisposition> dispositionsByObjectId)
        {
            if (track.Start is null)
            {
                return;
            }

            string displayName = GetDisplayName(track);

            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", displayName);
            builder.AppendElement("description", BuildTrackDescription(track, track.Start, options, dispositionsByObjectId));
            builder.AppendLine($"<styleUrl>{GetStartStyleUrl(track, options)}</styleUrl>");

            if (track.Start.AbsoluteTimeUtc is not null)
            {
                builder.AppendLine("<TimeStamp>");
                builder.AppendElement(
                    "when",
                    track.Start.AbsoluteTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                builder.AppendLine("</TimeStamp>");
            }

            builder.AppendLine("<Point>");
            builder.AppendElement("coordinates", FormatCoordinate(track.Start));
            builder.AppendLine("</Point>");
            builder.AppendLine("</Placemark>");
        }

        private static List<TacviewWeaponEmployment> CreateWeaponEmploymentsFromLifecycleReport(
    TacviewCombatReport combatReport,
    IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            return combatReport.WeaponLaunches
                .Select(launch => CreateWeaponEmploymentFromLifecycleLaunch(launch, objects))
                .Where(employment => employment is not null)
                .Cast<TacviewWeaponEmployment>()
                .ToList();
        }

        private static TacviewWeaponEmployment? CreateWeaponEmploymentFromLifecycleLaunch(
            TacviewWeaponLaunch launch,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            if (string.IsNullOrWhiteSpace(launch.WeaponObjectId))
            {
                return null;
            }

            if (!objects.TryGetValue(launch.WeaponObjectId, out TacviewObjectTrack? weaponTrack))
            {
                return null;
            }

            if (!IsRelevantWeaponObject(weaponTrack))
            {
                return null;
            }

            TacviewPositionSample? launchPosition =
                FindSampleClosestToTime(weaponTrack.Samples, launch.LaunchTimeSeconds)
                ?? weaponTrack.Start;

            if (launchPosition is null)
            {
                return null;
            }

            return new TacviewWeaponEmployment
            {
                WeaponObjectId = launch.WeaponObjectId,
                WeaponName = launch.WeaponName ?? weaponTrack.Name,
                WeaponType = launch.WeaponType ?? weaponTrack.Type,
                ParentObjectId = launch.LauncherObjectId,
                ParentName =
                    launch.LauncherName
                    ?? launch.LauncherPilot
                    ?? launch.LauncherObjectId,
                Position = launchPosition
            };
        }

        private static List<TacviewWeaponResult> CreateWeaponResultsFromLifecycleReport(
            TacviewCombatReport combatReport,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            DateTimeOffset? referenceTimeUtc)
        {
            return combatReport.TerminalEvents
                .Where(IsLifecycleObjectEffectResult)
                .Select(terminalEvent => CreateWeaponResultFromLifecycleTerminalEvent(
                    terminalEvent,
                    objects,
                    referenceTimeUtc))
                .Where(result => result is not null)
                .Cast<TacviewWeaponResult>()
                .ToList();
        }

        private static bool IsLifecycleObjectEffectResult(
            TacviewWeaponTerminalEvent terminalEvent)
        {
            if (string.IsNullOrWhiteSpace(terminalEvent.WeaponObjectId)
                || string.IsNullOrWhiteSpace(terminalEvent.TargetObjectId))
            {
                return false;
            }

            return terminalEvent.Outcome == TacviewTerminalOutcome.Hit
                || terminalEvent.Outcome == TacviewTerminalOutcome.Kill
                || terminalEvent.DestroyedTarget;
        }

        private static TacviewWeaponResult? CreateWeaponResultFromLifecycleTerminalEvent(
            TacviewWeaponTerminalEvent terminalEvent,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            DateTimeOffset? referenceTimeUtc)
        {
            if (string.IsNullOrWhiteSpace(terminalEvent.WeaponObjectId)
                || string.IsNullOrWhiteSpace(terminalEvent.TargetObjectId))
            {
                return null;
            }

            TacviewObjectTrack? weaponTrack = TryGetObjectTrack(
                objects,
                terminalEvent.WeaponObjectId);

            TacviewObjectTrack? targetTrack = TryGetObjectTrack(
                objects,
                terminalEvent.TargetObjectId);

            TacviewPositionSample? position = null;

            if (targetTrack is not null)
            {
                position = FindSampleClosestToTime(
                    targetTrack.Samples,
                    terminalEvent.TerminalTimeSeconds);
            }

            if (position is null && weaponTrack is not null)
            {
                position = FindSampleClosestToTime(
                    weaponTrack.Samples,
                    terminalEvent.TerminalTimeSeconds);
            }

            string eventType =
                terminalEvent.DestroyedTarget
                || terminalEvent.Outcome == TacviewTerminalOutcome.Kill
                    ? "Destroyed"
                    : "Hit";

            return new TacviewWeaponResult
            {
                EventType = eventType,
                TimeSeconds = terminalEvent.TerminalTimeSeconds,
                AbsoluteTimeUtc = ToAbsoluteTime(
                    referenceTimeUtc,
                    terminalEvent.TerminalTimeSeconds),
                SourceObjectId = terminalEvent.WeaponObjectId,
                SourceName = terminalEvent.WeaponName ?? weaponTrack?.Name,
                TargetObjectId = terminalEvent.TargetObjectId,
                TargetName =
                    terminalEvent.TargetName
                    ?? targetTrack?.Name
                    ?? targetTrack?.Pilot
                    ?? terminalEvent.TargetObjectId,
                Outcome = terminalEvent.Outcome.ToString(),
                Description = BuildLifecycleWeaponResultDescription(terminalEvent),
                Position = position
            };
        }

        private static TacviewObjectTrack? TryGetObjectTrack(
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            string? objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            return objects.TryGetValue(objectId, out TacviewObjectTrack? track)
                ? track
                : null;
        }

        private static DateTimeOffset? ToAbsoluteTime(
            DateTimeOffset? referenceTimeUtc,
            double timeSeconds)
        {
            return referenceTimeUtc?.AddSeconds(timeSeconds);
        }

        private static string BuildLifecycleWeaponResultDescription(
            TacviewWeaponTerminalEvent terminalEvent)
        {
            return
                $"Lifecycle correlation result\n" +
                $"Outcome: {terminalEvent.Outcome}\n" +
                $"Correlation Method: {terminalEvent.CorrelationMethod}\n" +
                $"Confidence: {terminalEvent.Confidence}\n" +
                $"Weapon: {terminalEvent.WeaponName ?? terminalEvent.WeaponObjectId}\n" +
                $"Launcher: {terminalEvent.LauncherName ?? terminalEvent.LauncherObjectId ?? "Unknown"}\n" +
                $"Target: {terminalEvent.TargetName ?? terminalEvent.TargetObjectId ?? "Unknown"}\n" +
                $"Target Distance: {(terminalEvent.TargetDistanceMeters?.ToString("F1", CultureInfo.InvariantCulture) ?? "Unknown")} m";
        }

        private static List<TacviewWeaponEmployment> MergeWeaponEmployments(
            IReadOnlyList<TacviewWeaponEmployment> oldWeaponEmployments,
            IReadOnlyList<TacviewWeaponEmployment> lifecycleWeaponEmployments,
            IReadOnlyList<TacviewWeaponResult> oldWeaponResults,
            IReadOnlyList<TacviewWeaponResult> lifecycleWeaponResults)
        {
            Dictionary<string, TacviewWeaponEmployment> lifecycleByWeaponId =
                lifecycleWeaponEmployments
                    .GroupBy(e => e.WeaponObjectId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First(),
                        StringComparer.OrdinalIgnoreCase);

            HashSet<string> oldObjectEffectWeaponIds = oldWeaponResults
                .Where(result => IsObjectEffectWeaponResultType(result.EventType))
                .Where(result => !string.IsNullOrWhiteSpace(result.SourceObjectId))
                .Select(result => result.SourceObjectId!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            HashSet<string> lifecycleObjectEffectWeaponIds = lifecycleWeaponResults
                .Where(result => IsObjectEffectWeaponResultType(result.EventType))
                .Where(result => !string.IsNullOrWhiteSpace(result.SourceObjectId))
                .Select(result => result.SourceObjectId!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<TacviewWeaponEmployment> merged = new();

            foreach (TacviewWeaponEmployment oldEmployment in oldWeaponEmployments)
            {
                if (!lifecycleByWeaponId.TryGetValue(
                        oldEmployment.WeaponObjectId,
                        out TacviewWeaponEmployment? lifecycleEmployment))
                {
                    merged.Add(oldEmployment);
                    continue;
                }

                // Existing Tacview parent data wins.
                if (!string.IsNullOrWhiteSpace(oldEmployment.ParentObjectId)
                    || !string.IsNullOrWhiteSpace(oldEmployment.ParentName))
                {
                    merged.Add(oldEmployment);
                    continue;
                }

                // Critical compatibility rule:
                // If Tacview already emitted an explicit object-effect event for this weapon,
                // do NOT infer a missing shooter from nearby birth geometry.
                // This preserves the existing "missing parent means Unknown" behavior.
                if (oldObjectEffectWeaponIds.Contains(oldEmployment.WeaponObjectId))
                {
                    merged.Add(oldEmployment);
                    continue;
                }

                // Lifecycle may fill shooter only when lifecycle is also supplying the
                // object-effect result for this weapon.
                if (!lifecycleObjectEffectWeaponIds.Contains(oldEmployment.WeaponObjectId))
                {
                    merged.Add(oldEmployment);
                    continue;
                }

                merged.Add(new TacviewWeaponEmployment
                {
                    WeaponObjectId = oldEmployment.WeaponObjectId,
                    WeaponName = oldEmployment.WeaponName ?? lifecycleEmployment.WeaponName,
                    WeaponType = oldEmployment.WeaponType ?? lifecycleEmployment.WeaponType,
                    ParentObjectId = lifecycleEmployment.ParentObjectId,
                    ParentName = lifecycleEmployment.ParentName,
                    Position = oldEmployment.Position
                });
            }

            HashSet<string> mergedWeaponIds = merged
                .Select(e => e.WeaponObjectId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (TacviewWeaponEmployment lifecycleEmployment in lifecycleWeaponEmployments)
            {
                if (mergedWeaponIds.Contains(lifecycleEmployment.WeaponObjectId))
                {
                    continue;
                }

                merged.Add(lifecycleEmployment);
            }

            return merged;
        }

        private static List<TacviewWeaponResult> MergeWeaponResults(
            IReadOnlyList<TacviewWeaponResult> oldWeaponResults,
            IReadOnlyList<TacviewWeaponResult> lifecycleWeaponResults)
        {
            List<TacviewWeaponResult> merged = new(oldWeaponResults);

            foreach (TacviewWeaponResult lifecycleResult in lifecycleWeaponResults)
            {
                bool alreadyRecorded = merged.Any(existing =>
                    IsEquivalentObjectEffectResult(existing, lifecycleResult));

                if (alreadyRecorded)
                {
                    continue;
                }

                merged.Add(lifecycleResult);
            }

            return merged;
        }

        private static bool IsEquivalentObjectEffectResult(
            TacviewWeaponResult existing,
            TacviewWeaponResult candidate)
        {
            if (!IsObjectEffectWeaponResultType(existing.EventType)
                || !IsObjectEffectWeaponResultType(candidate.EventType))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(existing.TargetObjectId)
                || string.IsNullOrWhiteSpace(candidate.TargetObjectId))
            {
                return false;
            }

            if (!existing.TargetObjectId.Equals(
                    candidate.TargetObjectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Explicit Tacview result wins. If the old parser already found an object-effect
            // result for the same target at the same time, lifecycle correlation must not add
            // a second Hit/Destroyed/Damage result on top of it.
            return Math.Abs(existing.TimeSeconds - candidate.TimeSeconds) <= 1.0;
        }

        private static List<TacViewWeaponEngagement> CreateWeaponEngagements(
            IReadOnlyList<TacviewObjectTrack> weaponTracks,
            IReadOnlyList<TacviewWeaponEmployment> weaponEmployments,
            IReadOnlyList<TacviewWeaponResult> weaponResults,
            out List<TacviewWeaponResult> unmatchedWeaponResults)
        {
            Dictionary<string, TacviewObjectTrack> tracksByWeaponId = weaponTracks
                .ToDictionary(track => track.ObjectId, StringComparer.OrdinalIgnoreCase);

            Dictionary<string, TacviewWeaponEmployment> employmentsByWeaponId = weaponEmployments
                .GroupBy(employment => employment.WeaponObjectId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<string, List<TacviewWeaponResult>> resultsByWeaponId =
                new(StringComparer.OrdinalIgnoreCase);

            unmatchedWeaponResults = new List<TacviewWeaponResult>();

            foreach (TacviewWeaponResult result in weaponResults)
            {
                string? weaponId = ResolveWeaponResultWeaponId(result, weaponTracks);

                if (!string.IsNullOrWhiteSpace(weaponId)
                    && tracksByWeaponId.ContainsKey(weaponId))
                {
                    if (!resultsByWeaponId.TryGetValue(weaponId, out List<TacviewWeaponResult>? resultsForWeapon))
                    {
                        resultsForWeapon = new List<TacviewWeaponResult>();
                        resultsByWeaponId[weaponId] = resultsForWeapon;
                    }

                    resultsForWeapon.Add(result);
                    continue;
                }

                unmatchedWeaponResults.Add(result);
            }

            return weaponTracks
                .Where(track => employmentsByWeaponId.ContainsKey(track.ObjectId))
                .Select(track => new TacViewWeaponEngagement
                {
                    WeaponTrack = track,
                    Employment = employmentsByWeaponId[track.ObjectId],
                    Results = resultsByWeaponId.TryGetValue(track.ObjectId, out List<TacviewWeaponResult>? results)
                        ? results
                        : Array.Empty<TacviewWeaponResult>()
                })
                .OrderBy(engagement => engagement.Employment.Position.TimeSeconds)
                .ThenBy(engagement => engagement.Employment.WeaponName ?? engagement.Employment.WeaponObjectId)
                .ToList();
        }

        private static string? ResolveWeaponResultWeaponId(
            TacviewWeaponResult result,
            IReadOnlyList<TacviewObjectTrack> weaponTracks)
        {
            if (!string.IsNullOrWhiteSpace(result.SourceObjectId)
                && weaponTracks.Any(track => track.ObjectId.Equals(result.SourceObjectId, StringComparison.OrdinalIgnoreCase)))
            {
                return result.SourceObjectId;
            }

            if (!string.IsNullOrWhiteSpace(result.TargetObjectId)
                && weaponTracks.Any(track => track.ObjectId.Equals(result.TargetObjectId, StringComparison.OrdinalIgnoreCase)))
            {
                return result.TargetObjectId;
            }

            if (result.Position is null)
            {
                return null;
            }

            var candidates = weaponTracks
                .Where(track => track.Start is not null)
                .Where(track => track.End is not null)
                .Where(track => track.Start!.TimeSeconds <= result.TimeSeconds + 0.01)
                .Select(track => new
                {
                    Track = track,
                    DistanceMeters = TacviewCombatClassifier.CalculateDistance3dMeters(track.End!, result.Position),
                    DeltaTimeSeconds = result.TimeSeconds - track.End!.TimeSeconds
                })
                .Where(candidate => candidate.DistanceMeters <= 15000)
                .Where(candidate => candidate.DeltaTimeSeconds >= -5 && candidate.DeltaTimeSeconds <= 300)
                .OrderBy(candidate => candidate.DistanceMeters)
                .ThenBy(candidate => Math.Abs(candidate.DeltaTimeSeconds))
                .ToList();

            return candidates.Count == 0
                ? null
                : candidates[0].Track.ObjectId;
        }
private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static void AppendDestroyedObjectsFolder(
            StringBuilder builder,
            IReadOnlyList<TacviewObjectTrack> groupTracks,
            IReadOnlyDictionary<string, ObjectDisposition> dispositionsByObjectId)
        {
            List<TacviewObjectTrack> destroyedTracks = groupTracks
                .Where(track => track.End is not null)
                .Where(track =>
                    dispositionsByObjectId.TryGetValue(track.ObjectId, out ObjectDisposition? disposition)
                    && disposition.WasDestroyed)
                .OrderBy(track => GetDisplayName(track), StringComparer.OrdinalIgnoreCase)
                .ThenBy(track => track.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (destroyedTracks.Count == 0)
            {
                return;
            }

            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Destroyed Objects</name>");

            foreach (TacviewObjectTrack track in destroyedTracks)
            {
                if (!dispositionsByObjectId.TryGetValue(track.ObjectId, out ObjectDisposition? disposition))
                {
                    continue;
                }

                AppendDestroyedObjectPlacemark(builder, track, disposition);
            }

            builder.AppendLine("</Folder>");
        }

        private static void AppendDestroyedObjectPlacemark(
            StringBuilder builder,
            TacviewObjectTrack track,
            ObjectDisposition disposition)
        {
            TacviewPositionSample? position =
                disposition.DestroyedAtSeconds is null
                    ? track.End
                    : FindSampleClosestToTime(track.Samples, disposition.DestroyedAtSeconds ?? 0);

            position ??= track.End;

            if (position is null)
            {
                return;
            }

            string displayName = GetDisplayName(track);

            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", $"Destroyed - {displayName}");
            builder.AppendElement("description", BuildDestroyedObjectDescription(track, disposition));
            builder.AppendLine("<styleUrl>#destroyedObjectStyle</styleUrl>");

            if (disposition.DestroyedAtUtc is not null)
            {
                builder.AppendLine("<TimeStamp>");
                builder.AppendElement(
                    "when",
                    disposition.DestroyedAtUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                builder.AppendLine("</TimeStamp>");
            }

            builder.AppendLine("<Point>");
            builder.AppendElement("coordinates", FormatCoordinate(position));
            builder.AppendLine("</Point>");
            builder.AppendLine("</Placemark>");
        }

        private static string BuildDestroyedObjectDescription(
            TacviewObjectTrack track,
            ObjectDisposition disposition)
        {
            string displayName = GetDisplayName(track);
            ObjectWeaponHit? killingHit = GetKillingHit(disposition);

            StringBuilder builder = new();

            builder.AppendLine($"Destroyed Object: {displayName} [{track.ObjectId}]");
            builder.AppendLine($"Name: {track.Name ?? "Unknown"}");
            builder.AppendLine($"Type: {track.Type ?? "Unknown"}");
            builder.AppendLine($"Group: {track.Group ?? "Unknown"}");
            builder.AppendLine($"Destroyed At: {FormatTime(disposition.DestroyedAtUtc, disposition.DestroyedAtSeconds ?? 0.0)}");

            if (killingHit is not null)
            {
                builder.AppendLine();
                builder.AppendLine($"Killed By Weapon: {killingHit.WeaponName} [{killingHit.WeaponObjectId}]");
                builder.AppendLine($"Shooter: {killingHit.ShooterName}");
                builder.AppendLine($"Target Object Id: {track.ObjectId}");
                builder.AppendLine($"Outcome: {killingHit.Outcome}");
            }

            return builder.ToString().TrimEnd();
        }

        private static ObjectWeaponHit? GetKillingHit(ObjectDisposition disposition)
        {
            if (disposition.WeaponHits.Count == 0)
            {
                return null;
            }

            ObjectWeaponHit? exactTimeHit = disposition.WeaponHits
                .Where(hit => Math.Abs((hit.HitTimeSeconds ?? 0.00) - (disposition.DestroyedAtSeconds ?? 0)) <= 0.25)
                .OrderByDescending(hit => hit.HitTimeSeconds)
                .FirstOrDefault();

            return exactTimeHit
                ?? disposition.WeaponHits
                    .OrderByDescending(hit => hit.HitTimeSeconds)
                    .FirstOrDefault();
        }

        private static void AppendWeaponsFolder(
            StringBuilder builder,
            IReadOnlyList<TacViewWeaponEngagement> engagements,
            IReadOnlyList<TacviewWeaponResult> unmatchedResults,
            PostBriefingKmlOptions options)
        {
            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Weapons</name>");

            foreach (TacViewWeaponEngagement engagement in engagements)
            {
                AppendWeaponEngagementFolder(builder, engagement, options);
            }

            if (unmatchedResults.Count > 0)
            {
                AppendUnmatchedWeaponResultsFolder(builder, unmatchedResults);
            }

            builder.AppendLine("</Folder>");
        }

        private static void AppendWeaponEngagementFolder(
            StringBuilder builder,
            TacViewWeaponEngagement engagement,
            PostBriefingKmlOptions options)
        {
            string weaponName = string.IsNullOrWhiteSpace(engagement.Employment.WeaponName)
                ? engagement.Employment.WeaponObjectId
                : engagement.Employment.WeaponName;

            string shooterName =
                engagement.Employment.ParentName
                ?? engagement.Employment.ParentObjectId
                ?? "Unknown";

            string folderName = string.Create(
                CultureInfo.InvariantCulture,
                $"{weaponName} - {shooterName} - {FormatTime(engagement.Employment.Position)}");

            AppendFolderStart(
                builder,
                folderName,
                visible: ShouldShowWeaponEngagementByDefault(engagement));

            AppendWeaponInformationFolder(builder, engagement, options);
            AppendWeaponShooterFolder(builder, engagement);
            AppendWeaponLaunchFolder(builder, engagement);
            AppendWeaponTrackFolder(builder, engagement, options);
            AppendWeaponResultsSubFolder(builder, engagement.Results);

            builder.AppendLine("</Folder>");
        }

        private static void AppendWeaponInformationFolder(
            StringBuilder builder,
            TacViewWeaponEngagement engagement,
            PostBriefingKmlOptions options)
        {
            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Weapon Information</name>");
            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", "Weapon Information");
            builder.AppendElement(
                "description",
                BuildWeaponEngagementDescription(engagement, options));
            builder.AppendLine("</Placemark>");
            builder.AppendLine("</Folder>");
        }

        private static string BuildWeaponEngagementDescription(
            TacViewWeaponEngagement engagement,
            PostBriefingKmlOptions options)
        {
            TacviewWeaponEmployment employment = engagement.Employment;
            TacviewObjectTrack weapon = engagement.WeaponTrack;

            return
                $"Weapon Object: {employment.WeaponObjectId}\n" +
                $"Weapon Name: {employment.WeaponName ?? "Unknown"}\n" +
                $"Weapon Type: {employment.WeaponType ?? "Unknown"}\n" +
                $"Weapon Kind: {GetWeaponEmploymentKind(employment)}\n" +
                $"Shooter: {employment.ParentName ?? employment.ParentObjectId ?? "Unknown"}\n" +
                $"Launch Time: {FormatTime(employment.Position)}\n" +
                $"Track Samples: {weapon.Samples.Count}\n" +
                $"Result Count: {engagement.Results.Count}\n\n" +
                BuildObjectDescription(weapon, options);
        }

        private static void AppendWeaponShooterFolder(
    StringBuilder builder,
    TacViewWeaponEngagement engagement)
        {
            TacviewWeaponEmployment employment = engagement.Employment;

            string shooterName =
                employment.ParentName
                ?? employment.ParentObjectId
                ?? "Unknown";

            string weaponName = string.IsNullOrWhiteSpace(employment.WeaponName)
                ? employment.WeaponObjectId
                : employment.WeaponName;

            string description =
                $"Shooter: {shooterName}\n" +
                $"Weapon: {weaponName}\n" +
                $"Weapon Object: {employment.WeaponObjectId}\n" +
                $"Weapon Type: {employment.WeaponType ?? "Unknown"}\n" +
                $"Time: {FormatTime(employment.Position)}\n\n" +
                "Position shown is the weapon launch position. If Tacview supplied a parent object, the shooter name is resolved from that parent.";

            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Launching Unit</name>");

            AppendPointPlacemark(
                builder,
                $"Shooter - {shooterName}",
                description,
                employment.Position,
                "#weaponPointStyle",
                visible: false);

            builder.AppendLine("</Folder>");
        }

        private static void AppendWeaponLaunchFolder(
            StringBuilder builder,
            TacViewWeaponEngagement engagement)
        {
            TacviewWeaponEmployment employment = engagement.Employment;

            string weaponName = string.IsNullOrWhiteSpace(employment.WeaponName)
                ? employment.WeaponObjectId
                : employment.WeaponName;

            string description =
                $"Weapon Object: {employment.WeaponObjectId}\n" +
                $"Weapon Type: {employment.WeaponType ?? "Unknown"}\n" +
                $"Weapon Kind: {GetWeaponEmploymentKind(employment)}\n" +
                $"Shooter: {employment.ParentName ?? employment.ParentObjectId ?? "Unknown"}\n" +
                $"Time: {FormatTime(employment.Position)}";

            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Launch Point</name>");

            AppendPointPlacemark(
                builder,
                weaponName,
                description,
                employment.Position,
                GetWeaponEmploymentStyleUrl(employment),
                visible: false);

            builder.AppendLine("</Folder>");
        }

        private static void AppendWeaponTrackFolder(
            StringBuilder builder,
            TacViewWeaponEngagement engagement,
            PostBriefingKmlOptions options)
        {
            TacviewObjectTrack weapon = engagement.WeaponTrack;

            if (weapon.Start is null)
            {
                return;
            }

            IReadOnlyList<TacviewPositionSample> sampledTrack =
                SelectEvenlyDistributedSamples(
                    weapon.Samples,
                    options.MaxTrackPointsPerObject);

            string displayName = !string.IsNullOrWhiteSpace(weapon.Name)
                ? weapon.Name
                : weapon.ObjectId;

            AppendFolderStart(
                builder,
                "Weapon Track",
                visible: false);

            if (sampledTrack.Count >= 2)
            {
                AppendLineStringPlacemark(
                    builder,
                    $"{displayName} Track",
                    BuildObjectDescription(weapon, options),
                    sampledTrack,
                    "#weaponTrackStyle");
            }

            if (weapon.End is not null && !ReferenceEquals(weapon.Start, weapon.End))
            {
                AppendPointPlacemark(
                    builder,
                    $"{displayName} Final Known Position",
                    $"Final known weapon position\n{BuildTrackDescription(weapon, weapon.End, options)}",
                    weapon.End,
                    "#weaponResultStyle");
            }

            builder.AppendLine("</Folder>");
        }

        private static void AppendWeaponResultsSubFolder(
            StringBuilder builder,
            IReadOnlyList<TacviewWeaponResult> results)
        {
            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Weapon Results</name>");

            if (results.Count == 0)
            {
                builder.AppendLine("<Placemark>");
                builder.AppendElement("name", "No matched weapon result");
                builder.AppendElement("description", "No destroyed or timeout event could be directly associated with this weapon.");
                builder.AppendLine("</Placemark>");
                builder.AppendLine("</Folder>");
                return;
            }

            foreach (TacviewWeaponResult result in results)
            {
                AppendWeaponResult(builder, result);
            }

            builder.AppendLine("</Folder>");
        }

        private static void AppendUnmatchedWeaponResultsFolder(
            StringBuilder builder,
            IReadOnlyList<TacviewWeaponResult> unmatchedResults)
        {
            builder.AppendLine("<Folder>");
            builder.AppendLine("<name>Unmatched Weapon Results</name>");

            foreach (TacviewWeaponResult result in unmatchedResults)
            {
                AppendWeaponResult(builder, result);
            }

            builder.AppendLine("</Folder>");
        }

        private static void AppendWeaponResult(
            StringBuilder builder,
            TacviewWeaponResult result)
        {
            string displayName = BuildWeaponResultDisplayName(result);
            string description = BuildWeaponResultDescription(result);
            bool visible = ShouldShowWeaponResultPlacemark(result);

            if (result.Position is not null)
            {
                AppendPointPlacemark(
                    builder,
                    displayName,
                    description,
                    result.Position,
                    "#weaponResultStyle",
                    visible);
                return;
            }

            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", displayName);

            if (!visible)
            {
                builder.AppendElement("visibility", "0");
            }

            builder.AppendElement("description", description);
            builder.AppendLine("</Placemark>");
        }

        private static bool ShouldShowWeaponResultPlacemark(TacviewWeaponResult result)
        {
            // Weapon result placemarks are useful in the KMZ tree, but visible markers
            // create too much clutter during large engagements.
            return false;
        }

        private static void AppendPointPlacemark(
            StringBuilder builder,
            string name,
            string description,
            TacviewPositionSample sample,
            string? styleUrl = null,
            bool visible = true)
        {
            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", name);
            builder.AppendElement("description", description);
            if (!visible)
            {
                builder.AppendElement("visibility", "0");
            }

            if (!string.IsNullOrWhiteSpace(styleUrl))
            {
                builder.AppendLine($"<styleUrl>{styleUrl}</styleUrl>");
            }

            if (sample.AbsoluteTimeUtc is not null)
            {
                builder.AppendLine("<TimeStamp>");
                builder.AppendElement(
                    "when",
                    sample.AbsoluteTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                builder.AppendLine("</TimeStamp>");
            }

            builder.AppendLine("<Point>");
            builder.AppendElement("coordinates", FormatCoordinate(sample));
            builder.AppendLine("</Point>");
            builder.AppendLine("</Placemark>");
        }

        private static void AppendTrackSamplePoints(
            StringBuilder builder,
            TacviewObjectTrack track,
            IReadOnlyList<TacviewPositionSample> sampledTrack,
            PostBriefingKmlOptions options)
        {
            if (sampledTrack.Count == 0)
            {
                return;
            }

            AppendFolderStart(
                builder,
                "Track Points",
                visible: false);

            string styleUrl = GetSamplePointStyleUrl(track, options);

            for (int i = 0; i < sampledTrack.Count; i++)
            {
                TacviewPositionSample sample = sampledTrack[i];

                builder.AppendLine("<Placemark>");
                builder.AppendElement("name", $"Point {i + 1}");
                builder.AppendElement(
                    "description",
                    $"Object: {GetDisplayName(track)}\n" +
                    $"Tactical Side: {GetCoalitionDisplayName(track, options)}\n" +
                    $"Tacview Coalition: {track.Coalition ?? "Unknown"}\n" +
                    $"Time: {FormatTime(sample)}\n" +
                    $"Lat/Lon: {sample.Latitude:F8}, {sample.Longitude:F8}\n" +
                    $"Alt: {(sample.AltitudeMeters ?? 0):F0} m");

                builder.AppendLine($"<styleUrl>{styleUrl}</styleUrl>");

                if (sample.AbsoluteTimeUtc is not null)
                {
                    builder.AppendLine("<TimeStamp>");
                    builder.AppendElement(
                        "when",
                        sample.AbsoluteTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                    builder.AppendLine("</TimeStamp>");
                }

                builder.AppendLine("<Point>");
                builder.AppendElement("coordinates", FormatCoordinate(sample));
                builder.AppendLine("</Point>");
                builder.AppendLine("</Placemark>");
            }

            builder.AppendLine("</Folder>");
        }



        private static string BuildWeaponResultDisplayName(TacviewWeaponResult result)
        {
            string subject =
                result.TargetName
                ?? result.TargetObjectId
                ?? result.SourceName
                ?? result.SourceObjectId
                ?? "Unknown";

            return $"{result.EventType} - {subject}";
        }

        private static string BuildWeaponResultDescription(TacviewWeaponResult result)
        {
            return
                $"Event: {result.EventType}\n" +
                $"Time: {FormatTime(result.AbsoluteTimeUtc, result.TimeSeconds)}\n" +
                $"Source: {result.SourceName ?? result.SourceObjectId ?? "Unknown"}\n" +
                $"Source Id: {result.SourceObjectId ?? "Unknown"}\n" +
                $"Target: {result.TargetName ?? result.TargetObjectId ?? "Unknown"}\n" +
                $"Target Id: {result.TargetObjectId ?? "Unknown"}\n" +
                $"Outcome: {result.Outcome ?? "Unknown"}\n" +
                $"Description: {result.Description ?? string.Empty}";
        }

        private static void AppendLineStringPlacemark(
            StringBuilder builder,
            string name,
            string description,
            IReadOnlyList<TacviewPositionSample> samples,
            string styleUrl)
        {
            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", name);
            builder.AppendElement("description", description);
            builder.AppendLine($"<styleUrl>{styleUrl}</styleUrl>");
            builder.AppendLine("<LineString>");
            builder.AppendLine("<tessellate>1</tessellate>");
            builder.AppendLine("<coordinates>");

            foreach (TacviewPositionSample sample in samples)
            {
                builder.AppendLine(FormatCoordinate(sample));
            }

            builder.AppendLine("</coordinates>");
            builder.AppendLine("</LineString>");
            builder.AppendLine("</Placemark>");
        }

        private static string BuildTrackDescription(
            TacviewObjectTrack track,
            TacviewPositionSample sample,
            PostBriefingKmlOptions options,
            IReadOnlyDictionary<string, ObjectDisposition>? dispositionsByObjectId = null)
        {
            return
                $"{BuildObjectDescription(track, options, dispositionsByObjectId)}\n" +
                $"Time: {FormatTime(sample)}";
        }
        private static string BuildObjectDescription(
            TacviewObjectTrack track,
            PostBriefingKmlOptions options,
            IReadOnlyDictionary<string, ObjectDisposition>? dispositionsByObjectId = null)
        {
            StringBuilder builder = new();

            ObjectDisposition? disposition = null;

            if (dispositionsByObjectId is not null)
            {
                dispositionsByObjectId.TryGetValue(track.ObjectId, out disposition);
            }

            builder.AppendLine($"Object Id: {track.ObjectId}");
            builder.AppendLine($"Name: {track.Name ?? "Unknown"}");
            builder.AppendLine($"Pilot: {track.Pilot ?? "Unknown"}");
            builder.AppendLine($"Type: {track.Type ?? "Unknown"}");
            builder.AppendLine($"Group: {track.Group ?? "Unknown"}");
            builder.AppendLine($"Tactical Side: {GetCoalitionDisplayName(track, options)}");
            builder.AppendLine($"Tacview Coalition: {track.Coalition ?? "Unknown"}");
            builder.AppendLine($"Tacview Color: {track.Color ?? "Unknown"}");

            if (track.Health is not null)
            {
                builder.AppendLine($"Health Remaining: {FormatHealth(track.Health)}");
            }
            else if (disposition is not null && disposition.WeaponHits.Count > 0)
            {
                builder.AppendLine("Health Remaining: Unknown / Not exported by Tacview");
            }
            else
            {
                builder.AppendLine("Health Remaining: Unknown");
            }

            builder.AppendLine();
            builder.AppendLine("Final Disposition:");

            if (disposition is null)
            {
                builder.AppendLine("Survived / No Weapon Result Recorded");
            }
            else
            {
                builder.AppendLine(disposition.WasDestroyed
                    ? "Destroyed"
                    : "Damaged / Weapon Effect Recorded");

                if (disposition.WasDestroyed)
                {
                    builder.AppendLine($"Destroyed At: {FormatTime(disposition.DestroyedAtUtc, disposition.DestroyedAtSeconds ?? 0.00)}");
                }

                if (!disposition.WasDestroyed && disposition.WeaponHits.Count > 0)
                {
                    ObjectWeaponHit lastHit = disposition.WeaponHits
                        .OrderByDescending(hit => hit.HitTimeSeconds)
                        .First();

                    builder.AppendLine();
                    builder.AppendLine("Damage Evidence:");
                    builder.AppendLine($"Tacview-recorded weapon hits: {disposition.WeaponHits.Count}");
                    builder.AppendLine($"Last recorded hit: {FormatTime(lastHit.HitTimeUtc, lastHit.HitTimeSeconds ?? 0)}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Weapons That Hit / Destroyed This Object:");

            if (disposition is null || disposition.WeaponHits.Count == 0)
            {
                builder.AppendLine("- None recorded");
            }
            else
            {
                foreach (ObjectWeaponHit hit in disposition.WeaponHits)
                {
                    builder.AppendLine(
                        $"- {hit.WeaponName} [{hit.WeaponObjectId}] from {hit.ShooterName} at {FormatTime(hit.HitTimeUtc, hit.HitTimeSeconds ?? 0)} - {hit.Outcome}");
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string GetDisplayName(TacviewObjectTrack track)
        {
            return TacviewObjectDisplayName.GetDisplayName(track);
        }

        private static string FormatCoordinate(TacviewPositionSample sample)
        {
            double altitude = sample.AltitudeMeters ?? 0;

            return string.Create(
                CultureInfo.InvariantCulture,
                $"{sample.Longitude:F8},{sample.Latitude:F8},{altitude:F2}");
        }

        private static string FormatTime(TacviewPositionSample sample)
        {
            return FormatTime(sample.AbsoluteTimeUtc, sample.TimeSeconds);
        }

        private static string FormatTime(DateTimeOffset? absoluteTimeUtc, double timeSeconds)
        {
            if (absoluteTimeUtc is not null)
            {
                return absoluteTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            }

            return string.Create(CultureInfo.InvariantCulture, $"T+{timeSeconds:F2}s");
        }

        private static string FormatHealth(double? health)
        {
            if (health is null)
            {
                return "Unknown";
            }

            double value = health.Value;

            if (value <= 1.0)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{value * 100.0:F0}%");
            }

            return string.Create(CultureInfo.InvariantCulture, $"{value:F0}%");
        }

        private static string GetTrackStyleUrl(
            TacviewObjectTrack track,
            PostBriefingKmlOptions options)
        {
            return GetCoalitionPrefix(track, options) switch
            {
                "blue" => "#blueTrackStyle",
                "red" => "#redTrackStyle",
                _ => "#neutralTrackStyle"
            };
        }

        private static string GetSamplePointStyleUrl(
            TacviewObjectTrack track,
            PostBriefingKmlOptions options)
        {
            return GetCoalitionPrefix(track, options) switch
            {
                "blue" => "#blueSamplePointStyle",
                "red" => "#redSamplePointStyle",
                _ => "#neutralSamplePointStyle"
            };
        }

        private static string GetStartStyleUrl(
    TacviewObjectTrack track,
    PostBriefingKmlOptions options)
        {
            string coalition = GetCoalitionPrefix(track, options);
            string objectKind = GetObjectKind(track);

            return objectKind switch
            {
                "plane" => coalition switch
                {
                    "blue" => "#bluePlaneStartStyle",
                    "red" => "#redPlaneStartStyle",
                    _ => "#neutralPlaneStartStyle"
                },

                "helo" => coalition switch
                {
                    "blue" => "#blueHeloStartStyle",
                    "red" => "#redHeloStartStyle",
                    _ => "#neutralHeloStartStyle"
                },

                "ship" => coalition switch
                {
                    "blue" => "#blueShipStartStyle",
                    "red" => "#redShipStartStyle",
                    _ => "#neutralShipStartStyle"
                },

                "sam" => coalition switch
                {
                    "blue" => "#blueSamStartStyle",
                    "red" => "#redSamStartStyle",
                    _ => "#neutralSamStartStyle"
                },

                _ => coalition switch
                {
                    "blue" => "#blueGroundStartStyle",
                    "red" => "#redGroundStartStyle",
                    _ => "#neutralGroundStartStyle"
                }
            };
        }


        private static string GetCoalitionPrefix(
            TacviewObjectTrack track,
            PostBriefingKmlOptions options)
        {
            // 1. Prefer explicit Tacview/DCS color if present.
            string? colorSide = TryInferSideFromTacviewColor(track.Color);
            if (colorSide is not null)
            {
                return colorSide;
            }

            // 2. Then use explicit coalition names only.
            string coalition = track.Coalition ?? string.Empty;

            if (IsExplicitBlueCoalition(coalition))
            {
                return "blue";
            }

            if (IsExplicitRedCoalition(coalition))
            {
                return "red";
            }

            // 3. Bullseye names usually contain Blue/Red.
            string? bullseyeCoalition = TryInferBullseyeCoalition(track);
            if (bullseyeCoalition is not null)
            {
                return bullseyeCoalition;
            }

            // 4. Known blue-force naval assets.
            if (options.InferBlueForKnownUsNavalAssets && LooksLikeBlueForceObject(track))
            {
                return "blue";
            }

            // 5. Conservative fallback.
            // Tacview Allies/Enemies are relative, so do NOT use them by default.
            if (IsTacviewAlliesCoalition(coalition))
            {
                return options.TreatTacviewAlliesAsBlue ? "blue" : "neutral";
            }

            if (IsTacviewEnemiesCoalition(coalition))
            {
                return options.TreatTacviewEnemiesAsRed ? "red" : "neutral";
            }

            // 6. Last-resort object-name inference.
            string? inferredSide = TryInferSideFromObjectIdentity(track);
            if (inferredSide is not null)
            {
                return inferredSide;
            }

            return "neutral";
        }
        private static bool IsExplicitBlueCoalition(string coalition)
        {
            return coalition.Equals("Blue", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("BlueFor", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("Blue Coalition", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExplicitRedCoalition(string coalition)
        {
            return coalition.Equals("Red", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("RedFor", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("Red Coalition", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTacviewAlliesCoalition(string coalition)
        {
            return coalition.Equals("Allies", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("Ally", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("Friendly", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTacviewEnemiesCoalition(string coalition)
        {
            return coalition.Equals("Enemies", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("Enemy", StringComparison.OrdinalIgnoreCase)
                || coalition.Equals("Hostile", StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryInferSideFromTacviewColor(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return null;
            }

            if (color.Contains("Blue", StringComparison.OrdinalIgnoreCase))
            {
                return "blue";
            }

            if (color.Contains("Red", StringComparison.OrdinalIgnoreCase))
            {
                return "red";
            }

            return null;
        }

        private static string? TryInferSideFromObjectIdentity(TacviewObjectTrack track)
        {
            string name = track.Name ?? string.Empty;
            string group = track.Group ?? string.Empty;
            string type = track.Type ?? string.Empty;

            string combined = $"{name} {group} {type}";

            if (LooksLikeBlueForceObject(track))
            {
                return "blue";
            }

            if (LooksLikeRedForceObject(combined))
            {
                return "red";
            }

            return null;
        }

        private static bool LooksLikeRedForceObject(string value)
        {
            return value.Contains("MiG-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Su-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Tu-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("IL-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("SA-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("S-300", StringComparison.OrdinalIgnoreCase)
                || value.Contains("S-400", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Buk", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Tor", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Tunguska", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Shilka", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCoalitionDisplayName(
            TacviewObjectTrack track,
            PostBriefingKmlOptions options)
        {
            return GetCoalitionPrefix(track, options) switch
            {
                "blue" => "Blue",
                "red" => "Red",
                _ => "Neutral/Unknown"
            };
        }

        private static bool LooksLikeBlueForceObject(TacviewObjectTrack track)
        {
            string name = track.Name ?? string.Empty;
            string group = track.Group ?? string.Empty;

            string combined = $"{name} {group}";

            return combined.Contains("CVN", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("USS", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Lincoln", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Roosevelt", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Washington", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Stennis", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Supercarrier", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBullseye(TacviewObjectTrack track)
        {
            string combined = $"{track.Name} {track.Group} {track.Type}";

            return combined.Contains("bullseye", StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryInferBullseyeCoalition(TacviewObjectTrack track)
        {
            if (!IsBullseye(track))
            {
                return null;
            }

            string combined = $"{track.Name} {track.Group} {track.Type}";

            if (combined.Contains("blue", StringComparison.OrdinalIgnoreCase))
            {
                return "blue";
            }

            if (combined.Contains("red", StringComparison.OrdinalIgnoreCase))
            {
                return "red";
            }

            return null;
        }

        private static string GetBullseyeStyleUrl(
            TacviewObjectTrack track,
            PostBriefingKmlOptions options)
        {
            return GetCoalitionPrefix(track, options) switch
            {
                "blue" => "#blueBullseyeStyle",
                "red" => "#redBullseyeStyle",
                _ => "#neutralBullseyeStyle"
            };
        }

        private static string GetBullseyeRingStyleUrl(
            TacviewObjectTrack track,
            PostBriefingKmlOptions options)
        {
            return GetCoalitionPrefix(track, options) switch
            {
                "blue" => "#blueBullseyeRingStyle",
                "red" => "#redBullseyeRingStyle",
                _ => "#neutralBullseyeRingStyle"
            };
        }

        private static void AppendBullseye(
            StringBuilder builder,
            TacviewObjectTrack track,
            PostBriefingKmlOptions options)
        {
            if (track.Start is null)
            {
                return;
            }

            string displayName = GetDisplayName(track);

            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", displayName);
            builder.AppendElement("description", BuildTrackDescription(track, track.Start, options));
            builder.AppendLine($"<styleUrl>{GetBullseyeStyleUrl(track, options)}</styleUrl>");

            if (track.Start.AbsoluteTimeUtc is not null)
            {
                builder.AppendLine("<TimeStamp>");
                builder.AppendElement(
                    "when",
                    track.Start.AbsoluteTimeUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                builder.AppendLine("</TimeStamp>");
            }

            builder.AppendLine("<Point>");
            builder.AppendElement("coordinates", FormatCoordinate(track.Start));
            builder.AppendLine("</Point>");
            builder.AppendLine("</Placemark>");

            AppendBullseyeRing(builder, track, options, 10);
            AppendBullseyeRing(builder, track, options, 25);
            AppendBullseyeRing(builder, track, options, 50);
        }

        private static void AppendBullseyeRing(
            StringBuilder builder,
            TacviewObjectTrack track,
            PostBriefingKmlOptions options,
            double radiusNm)
        {
            if (track.Start is null)
            {
                return;
            }

            string displayName = GetDisplayName(track);
            string coordinates = GenerateCircleCoordinates(
                track.Start.Latitude,
                track.Start.Longitude,
                radiusNm);

            builder.AppendLine("<Placemark>");
            builder.AppendElement("name", $"{displayName} {radiusNm:F0} NM Ring");
            builder.AppendLine($"<styleUrl>{GetBullseyeRingStyleUrl(track, options)}</styleUrl>");
            builder.AppendLine("<Polygon>");
            builder.AppendLine("<outerBoundaryIs>");
            builder.AppendLine("<LinearRing>");
            builder.AppendLine("<coordinates>");
            builder.AppendLine(coordinates);
            builder.AppendLine("</coordinates>");
            builder.AppendLine("</LinearRing>");
            builder.AppendLine("</outerBoundaryIs>");
            builder.AppendLine("</Polygon>");
            builder.AppendLine("</Placemark>");
        }

        private static string GenerateCircleCoordinates(
            double centerLat,
            double centerLon,
            double radiusNm,
            int segments = 72)
        {
            const double earthRadiusMeters = 6371000.0;

            double radiusMeters = radiusNm * 1852.0;
            double radiusRadians = radiusMeters / earthRadiusMeters;

            double centerLatRad = centerLat * Math.PI / 180.0;
            double centerLonRad = centerLon * Math.PI / 180.0;

            StringBuilder builder = new();

            for (int i = 0; i <= segments; i++)
            {
                double bearing = 2.0 * Math.PI * i / segments;

                double latRad = Math.Asin(
                    Math.Sin(centerLatRad) * Math.Cos(radiusRadians)
                    + Math.Cos(centerLatRad) * Math.Sin(radiusRadians) * Math.Cos(bearing));

                double lonRad = centerLonRad + Math.Atan2(
                    Math.Sin(bearing) * Math.Sin(radiusRadians) * Math.Cos(centerLatRad),
                    Math.Cos(radiusRadians) - Math.Sin(centerLatRad) * Math.Sin(latRad));

                double lat = latRad * 180.0 / Math.PI;
                double lon = lonRad * 180.0 / Math.PI;

                builder.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{lon:F8},{lat:F8},0"));
            }

            return builder.ToString();
        }

        private static string GetObjectKind(TacviewObjectTrack track)
        {
            HashSet<string> typeTokens = GetTacviewTypeTokens(track.Type);

            // Explicit Tacview Type tokens win.
            if (IsHelicopterType(typeTokens))
            {
                return "helo";
            }

            if (IsShipType(typeTokens))
            {
                return "ship";
            }

            if (IsPlaneType(typeTokens))
            {
                return "plane";
            }

            if (IsSamType(typeTokens, track))
            {
                return "sam";
            }

            // Fallback only for weak/missing Tacview Type values.
            return InferObjectKindFromNameAndGroup(track);
        }

        private static HashSet<string> GetTacviewTypeTokens(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return type
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsHelicopterType(IReadOnlySet<string> typeTokens)
        {
            return typeTokens.Contains("Rotorcraft")
                || typeTokens.Contains("Helicopter")
                || typeTokens.Contains("Helo");
        }

        private static bool IsPlaneType(IReadOnlySet<string> typeTokens)
        {
            return typeTokens.Contains("FixedWing")
                || typeTokens.Contains("Aircraft")
                || typeTokens.Contains("Airplane")
                || typeTokens.Contains("Plane");
        }

        private static bool IsShipType(IReadOnlySet<string> typeTokens)
        {
            return typeTokens.Contains("Sea")
                || typeTokens.Contains("Watercraft")
                || typeTokens.Contains("Ship")
                || typeTokens.Contains("Boat")
                || typeTokens.Contains("AircraftCarrier")
                || typeTokens.Contains("Frigate")
                || typeTokens.Contains("Destroyer")
                || typeTokens.Contains("Cruiser");
        }

        private static bool IsSamType(
            IReadOnlySet<string> typeTokens,
            TacviewObjectTrack track)
        {
            if (typeTokens.Contains("Air")
                || typeTokens.Contains("FixedWing")
                || typeTokens.Contains("Rotorcraft")
                || typeTokens.Contains("Sea")
                || typeTokens.Contains("Watercraft"))
            {
                return false;
            }

            string combined = $"{track.Type} {track.Name} {track.Group}";

            return LooksLikeSam(combined);
        }

        private static string InferObjectKindFromNameAndGroup(TacviewObjectTrack track)
        {
            string name = track.Name ?? string.Empty;
            string group = track.Group ?? string.Empty;
            string combined = $"{name} {group}";

            if (combined.Contains("Helicopter", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Rotorcraft", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Helo", StringComparison.OrdinalIgnoreCase))
            {
                return "helo";
            }

            if (combined.Contains("CVN", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("USS", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Ship", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Boat", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Carrier", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Frigate", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Destroyer", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Cruiser", StringComparison.OrdinalIgnoreCase))
            {
                return "ship";
            }

            if (combined.Contains("FixedWing", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Aircraft", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Plane", StringComparison.OrdinalIgnoreCase))
            {
                return "plane";
            }

            if (LooksLikeSam(combined))
            {
                return "sam";
            }

            return "ground";
        }

        private static string GetWeaponEmploymentStyleUrl(TacviewWeaponEmployment employment)
        {
            return GetWeaponEmploymentKind(employment) switch
            {
                "sam" => "#weaponEmploymentSamStyle",
                "bomb" => "#weaponEmploymentBombStyle",
                "bullet" => "#weaponEmploymentBulletStyle",
                _ => "#weaponEmploymentMissileStyle"
            };
        }

        private static string GetWeaponEmploymentKind(TacviewWeaponEmployment employment)
        {
            string combined =
                $"{employment.WeaponName} {employment.WeaponType}".Trim();

            if (LooksLikeSam(combined))
            {
                return "sam";
            }

            if (LooksLikeBomb(combined))
            {
                return "bomb";
            }

            if (LooksLikeBullet(combined))
            {
                return "bullet";
            }

            return "missile";
        }

        private static bool LooksLikeBomb(string value)
        {
            return value.Contains("bomb", StringComparison.OrdinalIgnoreCase)
                || value.Contains("gbu", StringComparison.OrdinalIgnoreCase)
                || value.Contains("mk-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("mk ", StringComparison.OrdinalIgnoreCase)
                || value.Contains("cbu", StringComparison.OrdinalIgnoreCase)
                || value.Contains("jdam", StringComparison.OrdinalIgnoreCase)
                || value.Contains("fab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeSam(string value)
        {
            return value.Contains("SAM", StringComparison.OrdinalIgnoreCase)
                || value.Contains("SA-", StringComparison.OrdinalIgnoreCase)
                || value.Contains("S-300", StringComparison.OrdinalIgnoreCase)
                || value.Contains("S-400", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Buk", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Tor", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Tunguska", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Kub", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Osa", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Hawk", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Patriot", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Rapier", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Roland", StringComparison.OrdinalIgnoreCase)
                || value.Contains("NASAMS", StringComparison.OrdinalIgnoreCase)
                || value.Contains("SM_2", StringComparison.OrdinalIgnoreCase)
                || value.Contains("SM-2", StringComparison.OrdinalIgnoreCase)
                || value.Contains("SM2", StringComparison.OrdinalIgnoreCase);
        }
        private static bool LooksLikeBullet(string value)
        {
            return value.Contains("bullet", StringComparison.OrdinalIgnoreCase)
                || value.Contains("shell", StringComparison.OrdinalIgnoreCase)
                || value.Contains("cannon", StringComparison.OrdinalIgnoreCase)
                || value.Contains("gun", StringComparison.OrdinalIgnoreCase)
                || value.Contains("round", StringComparison.OrdinalIgnoreCase)
                || value.Contains("projectile", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record AcmiParseResult(
            TacviewMissionInfo Mission,
            IReadOnlyList<TacviewObjectTrack> GroupTracks,
            IReadOnlyList<TacViewWeaponEngagement> WeaponEngagements,
            IReadOnlyList<TacviewWeaponResult> UnmatchedWeaponResults,
            DateTimeOffset? ReferenceTimeUtc);

        private sealed record ObjectDisposition(
            string ObjectId,
            bool WasDestroyed,
            DateTimeOffset? DestroyedAtUtc,
            double? DestroyedAtSeconds,
            IReadOnlyList<ObjectWeaponHit> WeaponHits);

        private sealed record ObjectWeaponHit(
            string WeaponName,
            string WeaponObjectId,
            string ShooterName,
            DateTimeOffset? HitTimeUtc,
            double? HitTimeSeconds,
            string Outcome,
            string Description);
    }

    internal static class StringBuilderXmlExtensions
    {
        public static void AppendElement(
            this StringBuilder builder,
            string elementName,
            string? value)
        {
            builder.Append('<');
            builder.Append(elementName);
            builder.Append('>');
            builder.Append(XmlEscape(value ?? string.Empty));
            builder.Append("</");
            builder.Append(elementName);
            builder.AppendLine(">");
        }

        private static string XmlEscape(string value)
        {
            return SecurityElementEscape(value);
        }

        private static string SecurityElementEscape(string value)
        {
            return value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&apos;", StringComparison.Ordinal);
        }

    }
}


