using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DcsMissionReader.Services
{
    public sealed class PostBriefingService() : IPostBriefingService
    {
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

            var parseResult = ParseZippedAcmi(acmiZipFilePath);

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
            AddIconToKmzIfAvailable(archive, "explode.svg");
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

        private AcmiParseResult ParseZippedAcmi(string zipFilePath)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);

            ZipArchiveEntry? directAcmiEntry = archive.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .FirstOrDefault(e =>
                    e.FullName.EndsWith(".acmi", StringComparison.OrdinalIgnoreCase)
                    && !e.FullName.EndsWith(".zip.acmi", StringComparison.OrdinalIgnoreCase));

            if (directAcmiEntry is not null)
            {
                using Stream stream = directAcmiEntry.Open();
                using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return ParseAcmi(reader);
            }

            ZipArchiveEntry? nestedZipEntry = archive.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .FirstOrDefault(e =>
                    e.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.EndsWith(".zip.acmi", StringComparison.OrdinalIgnoreCase));

            if (nestedZipEntry is not null)
            {
                using MemoryStream nestedZipBytes = new();

                using (Stream nestedZipStream = nestedZipEntry.Open())
                {
                    nestedZipStream.CopyTo(nestedZipBytes);
                }

                nestedZipBytes.Position = 0;

                using ZipArchive nestedArchive = new(nestedZipBytes, ZipArchiveMode.Read, leaveOpen: false);

                ZipArchiveEntry? nestedAcmiEntry = nestedArchive.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .FirstOrDefault(e =>
                        e.FullName.EndsWith(".acmi", StringComparison.OrdinalIgnoreCase)
                        && !e.FullName.EndsWith(".zip.acmi", StringComparison.OrdinalIgnoreCase))
                    ?? nestedArchive.Entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Name));

                if (nestedAcmiEntry is null)
                {
                    throw new InvalidDataException("The nested zip file does not contain an ACMI entry.");
                }

                using Stream stream = nestedAcmiEntry.Open();
                using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return ParseAcmi(reader);
            }

            ZipArchiveEntry? fallbackEntry = archive.Entries
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Name));

            if (fallbackEntry is null)
            {
                throw new InvalidDataException("The zip file does not contain an ACMI entry.");
            }

            using Stream fallbackStream = fallbackEntry.Open();
            using StreamReader fallbackReader = new(fallbackStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            return ParseAcmi(fallbackReader);
        }

        private AcmiParseResult ParseAcmi(TextReader reader)
        {
            Dictionary<string, TacviewObjectTrack> objects = new(StringComparer.OrdinalIgnoreCase);
            List<TacviewEventRecord> events = new();
            List<TacviewRemovalRecord> removals = new();
            List<TacviewHealthChangeRecord> healthChanges = new();
            TacviewMissionInfo mission = new();

            double currentTimeSeconds = 0;
            DateTimeOffset? referenceTimeUtc = null;
            double referenceLongitude = 0;
            double referenceLatitude = 0;

            foreach (string rawLine in ReadLogicalAcmiLines(reader))
            {
                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("FileType=", StringComparison.OrdinalIgnoreCase))
                {
                    mission.FileType = line["FileType=".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("FileVersion=", StringComparison.OrdinalIgnoreCase))
                {
                    mission.FileVersion = line["FileVersion=".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (double.TryParse(
                            line[1..],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double parsedTime))
                    {
                        currentTimeSeconds = parsedTime;
                    }

                    continue;
                }

                if (line.StartsWith("-", StringComparison.Ordinal))
                {
                    string removedObjectId = line[1..].Trim();

                    if (!string.IsNullOrWhiteSpace(removedObjectId))
                    {
                        removals.Add(new TacviewRemovalRecord(
                            removedObjectId,
                            currentTimeSeconds,
                            AddSeconds(referenceTimeUtc, currentTimeSeconds)));
                    }

                    continue;
                }

                int commaIndex = line.IndexOf(',');

                if (commaIndex <= 0)
                {
                    continue;
                }

                string objectId = line[..commaIndex].Trim();
                string payload = line[(commaIndex + 1)..].Trim();

                if (objectId == "0")
                {
                    ParseGlobalOrEventPayload(
                        payload,
                        currentTimeSeconds,
                        referenceTimeUtc,
                        events,
                        mission,
                        ref referenceLongitude,
                        ref referenceLatitude,
                        ref referenceTimeUtc);

                    continue;
                }

                TacviewObjectTrack track = GetOrCreateObject(objects, objectId);

                foreach (string token in SplitPropertyTokens(payload))
                {
                    int equalsIndex = token.IndexOf('=');

                    if (equalsIndex <= 0)
                    {
                        continue;
                    }

                    string key = token[..equalsIndex].Trim();
                    string value = token[(equalsIndex + 1)..].Trim();

                    ApplyObjectProperty(
                        track,
                        key,
                        value,
                        currentTimeSeconds,
                        referenceTimeUtc,
                        referenceLongitude,
                        referenceLatitude,
                        healthChanges);
                }
            }
            List<TacviewObjectTrack> groupTracks = objects.Values
                .Where(o => !ShouldSuppressFromObjectTracks(o))
                .Where(o => o.Samples.Count > 0)
                .OrderBy(o => o.Group ?? o.Name ?? o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<TacviewWeaponEmployment> weaponEmployments = objects.Values
                .Where(o => o.IsWeapon)
                .Where(IsRelevantWeaponObject)
                .Where(o => o.Start is not null)
                .Select(o => CreateWeaponEmployment(o, objects))
                .ToList();

            List<TacviewWeaponResult> explicitWeaponResults = events
                .Where(IsWeaponResultEventType)
                .Select(e => CreateWeaponResult(e, objects))
                .ToList();

            List<TacviewObjectTrack> weaponTracks = objects.Values
                .Where(IsRelevantWeaponObject)
                .Where(o => o.Samples.Count > 0)
                .OrderBy(o => o.Name ?? o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<TacviewWeaponResult> inferredDamageResults = new();

            inferredDamageResults.AddRange(
                CreateWeaponResultsFromHealthChanges(
                    healthChanges,
                    objects,
                    weaponTracks));

            HashSet<string> weaponIdsWithNonTimeoutResults =
                BuildWeaponIdsWithNonTimeoutResults(
                    explicitWeaponResults.Concat(inferredDamageResults),
                    weaponTracks);

            inferredDamageResults.AddRange(
                CreateWeaponResultsFromUnpairedWeaponRemovals(
                    removals,
                    objects,
                    weaponTracks,
                    weaponIdsWithNonTimeoutResults));

            weaponIdsWithNonTimeoutResults =
                BuildWeaponIdsWithNonTimeoutResults(
                    explicitWeaponResults.Concat(inferredDamageResults),
                    weaponTracks);

            List<TacviewWeaponResult> weaponResults = explicitWeaponResults
                .Concat(inferredDamageResults)
                .ToList();

            weaponResults.AddRange(
                CreateWeaponResultsFromRemovals(
                    removals,
                    objects,
                    weaponTracks,
                    weaponIdsWithNonTimeoutResults));

            List<TacViewWeaponEngagement> weaponEngagements = CreateWeaponEngagements(
                weaponTracks,
                weaponEmployments,
                weaponResults,
                out List<TacviewWeaponResult> unmatchedWeaponResults);

            return new AcmiParseResult(
                mission,
                groupTracks,
                weaponEngagements,
                unmatchedWeaponResults,
                referenceTimeUtc);
        }

        private static bool IsWeaponResultEventType(TacviewEventRecord eventRecord)
        {
            return eventRecord.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<TacviewWeaponResult> CreateWeaponResultsFromRemovals(
            IReadOnlyList<TacviewRemovalRecord> removals,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            IReadOnlyList<TacviewObjectTrack> weaponTracks,
            IReadOnlySet<string> weaponIdsWithNonTimeoutResults)
        {
            var results = new List<TacviewWeaponResult>();

            Dictionary<string, TacviewObjectTrack> weaponTracksById = weaponTracks
                .ToDictionary(w => w.ObjectId, StringComparer.OrdinalIgnoreCase);

            List<TacviewRemovalRecord> targetRemovals = removals
                .Where(r => objects.TryGetValue(r.ObjectId, out TacviewObjectTrack? removedObject)
                    && !removedObject.IsWeapon
                    && !IsSuppressedResultObject(removedObject))
                .ToList();

            foreach (TacviewRemovalRecord weaponRemoval in removals)
            {
                if (!weaponTracksById.TryGetValue(
                        weaponRemoval.ObjectId,
                        out TacviewObjectTrack? weapon))
                {
                    continue;
                }

                TacviewRemovalRecord? matchingTargetRemoval = targetRemovals
                    .Where(r => Math.Abs(r.TimeSeconds - weaponRemoval.TimeSeconds) <= 0.25)
                    .Where(r => !r.ObjectId.Equals(weaponRemoval.ObjectId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => Math.Abs(r.TimeSeconds - weaponRemoval.TimeSeconds))
                    .FirstOrDefault();

                if (matchingTargetRemoval is not null
                    && objects.TryGetValue(matchingTargetRemoval.ObjectId, out TacviewObjectTrack? target))
                {
                    results.Add(new TacviewWeaponResult
                    {
                        EventType = "Destroyed",
                        TimeSeconds = weaponRemoval.TimeSeconds,
                        AbsoluteTimeUtc = weaponRemoval.AbsoluteTimeUtc,
                        SourceObjectId = weapon.ObjectId,
                        SourceName = weapon.Name,
                        TargetObjectId = target.ObjectId,
                        TargetName = GetDisplayName(target),
                        Outcome = "Object removed at same Tacview time as weapon",
                        Description = $"Synthesized from Tacview removal records: -{weapon.ObjectId} and -{target.ObjectId}",
                        Position = target.End ?? weapon.End
                    });

                    continue;
                }

                if (weaponIdsWithNonTimeoutResults.Contains(weapon.ObjectId))
                {
                    continue;
                }

                results.Add(new TacviewWeaponResult
                {
                    EventType = "Timeout",
                    TimeSeconds = weaponRemoval.TimeSeconds,
                    AbsoluteTimeUtc = weaponRemoval.AbsoluteTimeUtc,
                    SourceObjectId = weapon.ObjectId,
                    SourceName = weapon.Name,
                    TargetObjectId = null,
                    TargetName = null,
                    Outcome = "Weapon removed without matching target removal",
                    Description = $"Synthesized from Tacview removal record: -{weapon.ObjectId}",
                    Position = weapon.End
                });
            }

            return results;
        }


        private static IReadOnlyList<TacviewWeaponResult> CreateWeaponResultsFromHealthChanges(
            IReadOnlyList<TacviewHealthChangeRecord> healthChanges,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            IReadOnlyList<TacviewObjectTrack> weaponTracks)
        {
            var results = new List<TacviewWeaponResult>();

            foreach (TacviewHealthChangeRecord healthChange in healthChanges)
            {
                if (!objects.TryGetValue(healthChange.ObjectId, out TacviewObjectTrack? target))
                {
                    continue;
                }

                InferredDamageMatch? match = FindBestWeaponForHealthDrop(
                    healthChange,
                    target,
                    weaponTracks);

                if (match is null)
                {
                    continue;
                }

                results.Add(new TacviewWeaponResult
                {
                    EventType = "Damage",
                    TimeSeconds = healthChange.TimeSeconds,
                    AbsoluteTimeUtc = healthChange.AbsoluteTimeUtc,
                    SourceObjectId = match.Weapon.ObjectId,
                    SourceName = match.Weapon.Name,
                    TargetObjectId = target.ObjectId,
                    TargetName = GetDisplayName(target),
                    Outcome = string.Create(
                        CultureInfo.InvariantCulture,
                        $"Inferred from target health drop {FormatHealth(healthChange.PreviousHealth)} -> {FormatHealth(healthChange.NewHealth)}"),
                    Description = string.Create(
                        CultureInfo.InvariantCulture,
                        $"Target health dropped from {FormatHealth(healthChange.PreviousHealth)} to {FormatHealth(healthChange.NewHealth)} near weapon {match.Weapon.ObjectId}; distance {match.DistanceMeters:F0} m"),
                    Position = healthChange.Position ?? match.TargetPosition
                });
            }

            return results;
        }

        private static IReadOnlyList<TacviewWeaponResult> CreateWeaponResultsFromUnpairedWeaponRemovals(
            IReadOnlyList<TacviewRemovalRecord> removals,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            IReadOnlyList<TacviewObjectTrack> weaponTracks,
            IReadOnlySet<string> weaponIdsWithNonTimeoutResults)
        {
            var results = new List<TacviewWeaponResult>();

            Dictionary<string, TacviewObjectTrack> weaponTracksById = weaponTracks
                .ToDictionary(w => w.ObjectId, StringComparer.OrdinalIgnoreCase);

            foreach (TacviewRemovalRecord weaponRemoval in removals)
            {
                if (!weaponTracksById.TryGetValue(
                        weaponRemoval.ObjectId,
                        out TacviewObjectTrack? weapon))
                {
                    continue;
                }

                if (weaponIdsWithNonTimeoutResults.Contains(weapon.ObjectId))
                {
                    continue;
                }

                if (HasSynchronizedTargetRemoval(weaponRemoval, removals, objects))
                {
                    continue;
                }

                InferredDamageMatch? match = FindBestUnpairedRemovalDamageTarget(
                    weapon,
                    weaponRemoval,
                    removals,
                    objects.Values,
                    weaponTracksById);

                if (match is null)
                {
                    continue;
                }

                results.Add(new TacviewWeaponResult
                {
                    EventType = "Damage",
                    TimeSeconds = weaponRemoval.TimeSeconds,
                    AbsoluteTimeUtc = weaponRemoval.AbsoluteTimeUtc,
                    SourceObjectId = weapon.ObjectId,
                    SourceName = weapon.Name,
                    TargetObjectId = match.Target.ObjectId,
                    TargetName = GetDisplayName(match.Target),
                    Outcome = "Inferred from unpaired opposing weapon removal near target",
                    Description = string.Create(
                        CultureInfo.InvariantCulture,
                        $"Weapon {weapon.ObjectId} removed near opposing target {match.Target.ObjectId}; distance {match.DistanceMeters:F0} m; no paired defensive weapon removal found"),
                    Position = match.TargetPosition
                });
            }

            return results;
        }

        private static HashSet<string> BuildWeaponIdsWithNonTimeoutResults(
            IEnumerable<TacviewWeaponResult> results,
            IReadOnlyList<TacviewObjectTrack> weaponTracks)
        {
            HashSet<string> weaponIds = weaponTracks
                .Select(track => track.ObjectId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            HashSet<string> resultWeaponIds = new(StringComparer.OrdinalIgnoreCase);

            foreach (TacviewWeaponResult result in results)
            {
                if (result.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(result.SourceObjectId)
                    && weaponIds.Contains(result.SourceObjectId))
                {
                    resultWeaponIds.Add(result.SourceObjectId);
                }

                if (!string.IsNullOrWhiteSpace(result.TargetObjectId)
                    && weaponIds.Contains(result.TargetObjectId))
                {
                    resultWeaponIds.Add(result.TargetObjectId);
                }
            }

            return resultWeaponIds;
        }

        private static InferredDamageMatch? FindBestWeaponForHealthDrop(
            TacviewHealthChangeRecord healthChange,
            TacviewObjectTrack target,
            IReadOnlyList<TacviewObjectTrack> weaponTracks)
        {
            TacviewPositionSample? targetPosition =
                healthChange.Position
                ?? target.End
                ?? FindSampleClosestToTime(target.Samples, healthChange.TimeSeconds);

            if (targetPosition is null)
            {
                return null;
            }

            const double maxDamageDistanceMeters = 5_000.0;
            const double maxWeaponTimeBeforeDamageSeconds = 20.0;
            const double maxWeaponTimeAfterDamageSeconds = 3.0;

            return weaponTracks
                .Where(weapon => weapon.End is not null)
                .Where(weapon => IsPotentialDamageTargetForWeapon(weapon, target))
                .Select(weapon => new InferredDamageMatch(
                    weapon,
                    target,
                    targetPosition,
                    CalculateDistanceMeters(weapon.End!, targetPosition),
                    healthChange.TimeSeconds - weapon.End!.TimeSeconds))
                .Where(match => match.DistanceMeters <= maxDamageDistanceMeters)
                .Where(match => match.DeltaTimeSeconds >= -maxWeaponTimeAfterDamageSeconds
                    && match.DeltaTimeSeconds <= maxWeaponTimeBeforeDamageSeconds)
                .OrderBy(match => match.DistanceMeters)
                .ThenBy(match => Math.Abs(match.DeltaTimeSeconds))
                .FirstOrDefault();
        }

        private static InferredDamageMatch? FindBestUnpairedRemovalDamageTarget(
            TacviewObjectTrack weapon,
            TacviewRemovalRecord weaponRemoval,
            IReadOnlyList<TacviewRemovalRecord> removals,
            IEnumerable<TacviewObjectTrack> objects,
            IReadOnlyDictionary<string, TacviewObjectTrack> weaponTracksById)
        {
            TacviewPositionSample? weaponPosition =
                weapon.End
                ?? FindSampleClosestToTime(weapon.Samples, weaponRemoval.TimeSeconds);

            if (weaponPosition is null)
            {
                return null;
            }

            const double maxInferredDamageDistanceMeters = 3_000.0;
            const double maxTargetSampleTimeDifferenceSeconds = 15.0;

            InferredDamageMatch? bestMatch = null;

            foreach (TacviewObjectTrack target in objects)
            {
                if (target.ObjectId.Equals(weapon.ObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (target.IsWeapon)
                {
                    continue;
                }

                if (target.Samples.Count == 0)
                {
                    continue;
                }

                if (IsSuppressedResultObject(target))
                {
                    continue;
                }

                if (!IsPotentialDamageTargetForWeapon(weapon, target))
                {
                    continue;
                }

                TacviewPositionSample? targetPosition =
                    FindSampleClosestToTime(target.Samples, weaponRemoval.TimeSeconds);

                if (targetPosition is null)
                {
                    continue;
                }

                double targetTimeDifference = Math.Abs(targetPosition.TimeSeconds - weaponRemoval.TimeSeconds);

                if (targetTimeDifference > maxTargetSampleTimeDifferenceSeconds)
                {
                    continue;
                }

                double distanceMeters = CalculateDistanceMeters(weaponPosition, targetPosition);

                if (distanceMeters > maxInferredDamageDistanceMeters)
                {
                    continue;
                }

                if (HasNearbyDefensiveWeaponRemoval(
                        weapon,
                        weaponPosition,
                        weaponRemoval,
                        target,
                        removals,
                        weaponTracksById))
                {
                    continue;
                }

                var match = new InferredDamageMatch(
                    weapon,
                    target,
                    targetPosition,
                    distanceMeters,
                    weaponRemoval.TimeSeconds - weaponPosition.TimeSeconds);

                if (bestMatch is null
                    || match.DistanceMeters < bestMatch.DistanceMeters
                    || (Math.Abs(match.DistanceMeters - bestMatch.DistanceMeters) < 0.001
                        && Math.Abs(match.DeltaTimeSeconds) < Math.Abs(bestMatch.DeltaTimeSeconds)))
                {
                    bestMatch = match;
                }
            }

            return bestMatch;
        }

        private static bool HasSynchronizedTargetRemoval(
            TacviewRemovalRecord weaponRemoval,
            IReadOnlyList<TacviewRemovalRecord> removals,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            return removals
                .Where(r => Math.Abs(r.TimeSeconds - weaponRemoval.TimeSeconds) <= 0.25)
                .Where(r => !r.ObjectId.Equals(weaponRemoval.ObjectId, StringComparison.OrdinalIgnoreCase))
                .Any(r => objects.TryGetValue(r.ObjectId, out TacviewObjectTrack? removedObject)
                    && !removedObject.IsWeapon
                    && !IsSuppressedResultObject(removedObject));
        }

        private static bool HasNearbyDefensiveWeaponRemoval(
            TacviewObjectTrack weapon,
            TacviewPositionSample weaponPosition,
            TacviewRemovalRecord weaponRemoval,
            TacviewObjectTrack target,
            IReadOnlyList<TacviewRemovalRecord> removals,
            IReadOnlyDictionary<string, TacviewObjectTrack> weaponTracksById)
        {
            const double maxDefensivePairTimeDifferenceSeconds = 0.75;
            const double maxDefensivePairDistanceMeters = 750.0;

            foreach (TacviewRemovalRecord otherRemoval in removals)
            {
                if (otherRemoval.ObjectId.Equals(weaponRemoval.ObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Math.Abs(otherRemoval.TimeSeconds - weaponRemoval.TimeSeconds) > maxDefensivePairTimeDifferenceSeconds)
                {
                    continue;
                }

                if (!weaponTracksById.TryGetValue(otherRemoval.ObjectId, out TacviewObjectTrack? otherWeapon))
                {
                    continue;
                }

                if (!AreSameKnownCoalition(otherWeapon.Coalition, target.Coalition))
                {
                    continue;
                }

                if (AreSameKnownCoalition(otherWeapon.Coalition, weapon.Coalition))
                {
                    continue;
                }

                TacviewPositionSample? otherWeaponPosition =
                    otherWeapon.End
                    ?? FindSampleClosestToTime(otherWeapon.Samples, otherRemoval.TimeSeconds);

                if (otherWeaponPosition is null)
                {
                    continue;
                }

                double distanceMeters = CalculateDistanceMeters(weaponPosition, otherWeaponPosition);

                if (distanceMeters <= maxDefensivePairDistanceMeters)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPotentialDamageTargetForWeapon(
            TacviewObjectTrack weapon,
            TacviewObjectTrack target)
        {
            if (!weapon.IsWeapon || target.IsWeapon)
            {
                return false;
            }

            if (AreSameKnownCoalition(weapon.Coalition, target.Coalition))
            {
                return false;
            }

            string weaponText = $"{weapon.Name} {weapon.Type} {weapon.Group}";
            string targetText = $"{target.Name} {target.Type} {target.Group}";

            if (IsCountermeasureOrDecoy(weaponText)
                || IsJettisonedStore(weaponText)
                || IsGunRoundOrShell(weaponText))
            {
                return false;
            }

            if (IsCountermeasureOrDecoy(targetText)
                || IsJettisonedStore(targetText))
            {
                return false;
            }

            return true;
        }

        private static bool AreSameKnownCoalition(string? first, string? second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            {
                return false;
            }

            return first.Trim().Equals(second.Trim(), StringComparison.OrdinalIgnoreCase);
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
        private static void ParseGlobalOrEventPayload(
            string payload,
            double currentTimeSeconds,
            DateTimeOffset? currentReferenceTimeUtc,
            List<TacviewEventRecord> events,
            TacviewMissionInfo mission,
            ref double referenceLongitude,
            ref double referenceLatitude,
            ref DateTimeOffset? referenceTimeUtc)
        {
            foreach (string token in SplitPropertyTokens(payload))
            {
                int equalsIndex = token.IndexOf('=');

                if (equalsIndex <= 0)
                {
                    continue;
                }

                string key = token[..equalsIndex].Trim();
                string value = token[(equalsIndex + 1)..].Trim();

                if (key.Equals("ReferenceTime", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTimeOffset.TryParse(
                            value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out DateTimeOffset parsedReferenceTime))
                    {
                        referenceTimeUtc = parsedReferenceTime;
                        mission.ReferenceTimeUtc = parsedReferenceTime;
                    }

                    continue;
                }

                if (key.Equals("RecordingTime", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTimeOffset.TryParse(
                            value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out DateTimeOffset parsedRecordingTime))
                    {
                        mission.RecordingTimeUtc = parsedRecordingTime;
                    }

                    continue;
                }

                if (key.Equals("ReferenceLongitude", StringComparison.OrdinalIgnoreCase))
                {
                    referenceLongitude = ParseDoubleOrDefault(value);
                    mission.ReferenceLongitude = referenceLongitude;
                    continue;
                }

                if (key.Equals("ReferenceLatitude", StringComparison.OrdinalIgnoreCase))
                {
                    referenceLatitude = ParseDoubleOrDefault(value);
                    mission.ReferenceLatitude = referenceLatitude;
                    continue;
                }

                if (key.Equals("Title", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Title = value;
                    continue;
                }

                if (key.Equals("DataRecorder", StringComparison.OrdinalIgnoreCase))
                {
                    mission.DataRecorder = value;
                    continue;
                }

                if (key.Equals("DataSource", StringComparison.OrdinalIgnoreCase))
                {
                    mission.DataSource = value;
                    continue;
                }

                if (key.Equals("Author", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Author = value;
                    continue;
                }

                if (key.Equals("Comments", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Comments = value;
                    continue;
                }

                if (key.Equals("Category", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Category = value;
                    continue;
                }

                if (key.Equals("Briefing", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Briefing = value;
                    continue;
                }

                if (key.Equals("Event", StringComparison.OrdinalIgnoreCase))
                {
                    events.Add(ParseEvent(value, currentTimeSeconds, currentReferenceTimeUtc));
                    continue;
                }
            }
        }

        private static IEnumerable<string> ReadLogicalAcmiLines(TextReader reader)
        {
            StringBuilder current = new();

            while (reader.ReadLine() is string line)
            {
                bool continues = line.EndsWith("\\", StringComparison.Ordinal);

                if (continues)
                {
                    current.Append(line[..^1]);
                    current.AppendLine();
                    continue;
                }

                if (current.Length == 0)
                {
                    yield return line;
                    continue;
                }

                current.Append(line);
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        private static TacviewEventRecord ParseEvent(
            string value,
            double currentTimeSeconds,
            DateTimeOffset? referenceTimeUtc)
        {
            string[] parts = value
                .Split('|', StringSplitOptions.None)
                .Select(p => p.Trim())
                .ToArray();

            string eventType = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])
                ? parts[0]
                : "Unknown";

            string? text = parts.Length > 1 ? parts[^1] : null;

            return new TacviewEventRecord
            {
                TimeSeconds = currentTimeSeconds,
                AbsoluteTimeUtc = AddSeconds(referenceTimeUtc, currentTimeSeconds),
                EventType = eventType,
                Parts = parts,
                Text = text
            };
        }

        private static TacviewObjectTrack GetOrCreateObject(
            Dictionary<string, TacviewObjectTrack> objects,
            string objectId)
        {
            if (objects.TryGetValue(objectId, out TacviewObjectTrack? existing))
            {
                return existing;
            }

            var created = new TacviewObjectTrack
            {
                ObjectId = objectId
            };

            objects[objectId] = created;

            return created;
        }

        private static void ApplyObjectProperty(
            TacviewObjectTrack track,
            string key,
            string value,
            double currentTimeSeconds,
            DateTimeOffset? referenceTimeUtc,
            double referenceLongitude,
            double referenceLatitude,
            List<TacviewHealthChangeRecord> healthChanges)
        {
            switch (key)
            {
                case "Name":
                    track.Name = value;
                    break;

                case "Type":
                    track.Type = value;
                    break;

                case "Group":
                    track.Group = value;
                    break;

                case "Parent":
                    track.ParentObjectId = value;
                    break;

                case "Coalition":
                    track.Coalition = value;
                    break;

                case "Color":
                    track.Color = value;
                    break;

                case "Health":
                    if (double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double health))
                    {
                        double? previousHealth = track.Health;
                        track.Health = health;

                        if (previousHealth is not null && health < previousHealth.Value)
                        {
                            healthChanges.Add(new TacviewHealthChangeRecord(
                                track.ObjectId,
                                previousHealth.Value,
                                health,
                                currentTimeSeconds,
                                AddSeconds(referenceTimeUtc, currentTimeSeconds),
                                track.End));
                        }
                    }
                    break;

                case "T":
                    TacviewPositionSample? sample = ParseTransform(
                        value,
                        currentTimeSeconds,
                        referenceTimeUtc,
                        referenceLongitude,
                        referenceLatitude);

                    if (sample is not null)
                    {
                        track.Samples.Add(sample);
                    }

                    break;
            }
        }

        private static TacviewPositionSample? ParseTransform(
            string value,
            double currentTimeSeconds,
            DateTimeOffset? referenceTimeUtc,
            double referenceLongitude,
            double referenceLatitude)
        {
            string[] parts = value.Split('|', StringSplitOptions.None);

            if (parts.Length < 2)
            {
                return null;
            }

            if (!TryParseNullableDouble(parts[0], out double? lonOffset)
                || !TryParseNullableDouble(parts[1], out double? latOffset))
            {
                return null;
            }

            if (lonOffset is null || latOffset is null)
            {
                return null;
            }

            double? altitude = null;

            if (parts.Length >= 3 && TryParseNullableDouble(parts[2], out double? parsedAltitude))
            {
                altitude = parsedAltitude;
            }

            return new TacviewPositionSample
            {
                TimeSeconds = currentTimeSeconds,
                AbsoluteTimeUtc = AddSeconds(referenceTimeUtc, currentTimeSeconds),
                Longitude = referenceLongitude + lonOffset.Value,
                Latitude = referenceLatitude + latOffset.Value,
                AltitudeMeters = altitude
            };
        }

        private static TacviewWeaponEmployment CreateWeaponEmployment(
         TacviewObjectTrack weapon,
         IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            TacviewObjectTrack? shooter = ResolveWeaponShooter(weapon, objects);

            return new TacviewWeaponEmployment
            {
                WeaponObjectId = weapon.ObjectId,
                WeaponName = weapon.Name,
                WeaponType = weapon.Type,
                ParentObjectId = weapon.ParentObjectId,

                ParentName = shooter is null
                    ? null
                    : GetDisplayName(shooter),

                Position = weapon.Start!
            };
        }

        private static TacviewObjectTrack? ResolveWeaponShooter(
    TacviewObjectTrack weapon,
    IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            if (!string.IsNullOrWhiteSpace(weapon.ParentObjectId))
            {
                string parentId = weapon.ParentObjectId.Trim();

                if (objects.TryGetValue(parentId, out TacviewObjectTrack? directParent))
                {
                    return directParent;
                }

                string normalizedParentId = NormalizeTacviewObjectId(parentId);

                TacviewObjectTrack? normalizedParent = objects
                    .FirstOrDefault(pair =>
                        NormalizeTacviewObjectId(pair.Key)
                            .Equals(normalizedParentId, StringComparison.OrdinalIgnoreCase))
                    .Value;

                if (normalizedParent is not null)
                {
                    return normalizedParent;
                }
            }

            return FindClosestLikelyShooterAtLaunch(weapon, objects.Values);
        }

        private static string NormalizeTacviewObjectId(string value)
        {
            return value
                .Trim()
                .TrimStart('#')
                .Trim('{', '}')
                .Trim();
        }

        private static TacviewObjectTrack? FindClosestLikelyShooterAtLaunch(
            TacviewObjectTrack weapon,
            IEnumerable<TacviewObjectTrack> objects)
        {
            if (weapon.Start is null)
            {
                return null;
            }

            const double maxShooterDistanceMeters = 2_000.0;
            const double maxTimeDifferenceSeconds = 5.0;

            TacviewObjectTrack? bestObject = null;
            double bestDistanceMeters = double.MaxValue;

            foreach (TacviewObjectTrack candidate in objects)
            {
                if (candidate.ObjectId.Equals(weapon.ObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (candidate.IsWeapon)
                {
                    continue;
                }

                if (candidate.Samples.Count == 0)
                {
                    continue;
                }

                TacviewPositionSample? candidateSample =
                    FindSampleClosestToTime(candidate.Samples, weapon.Start.TimeSeconds);

                if (candidateSample is null)
                {
                    continue;
                }

                double timeDifference = Math.Abs(candidateSample.TimeSeconds - weapon.Start.TimeSeconds);

                if (timeDifference > maxTimeDifferenceSeconds)
                {
                    continue;
                }

                double distanceMeters = CalculateDistanceMeters(
                    weapon.Start,
                    candidateSample);

                if (distanceMeters > maxShooterDistanceMeters)
                {
                    continue;
                }

                if (distanceMeters < bestDistanceMeters)
                {
                    bestDistanceMeters = distanceMeters;
                    bestObject = candidate;
                }
            }

            return bestObject;
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

        private static TacviewWeaponResult CreateWeaponResult(
            TacviewEventRecord eventRecord,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            string? sourceObjectId = null;
            string? targetObjectId = null;
            string? outcome = null;

            foreach (string part in eventRecord.Parts)
            {
                if (part.StartsWith("SourceId:", StringComparison.OrdinalIgnoreCase))
                {
                    sourceObjectId = part["SourceId:".Length..].Trim();
                }
                else if (part.StartsWith("TargetId:", StringComparison.OrdinalIgnoreCase))
                {
                    targetObjectId = part["TargetId:".Length..].Trim();
                }
                else if (part.StartsWith("ObjectId:", StringComparison.OrdinalIgnoreCase))
                {
                    targetObjectId ??= part["ObjectId:".Length..].Trim();
                }
                else if (part.StartsWith("VictimId:", StringComparison.OrdinalIgnoreCase))
                {
                    targetObjectId ??= part["VictimId:".Length..].Trim();
                }
                else if (part.StartsWith("Outcome:", StringComparison.OrdinalIgnoreCase))
                {
                    outcome = part["Outcome:".Length..].Trim();
                }
            }

            // Tacview often emits compact forms like:
            // Event=Destroyed|300|Target destroyed
            // Event=Timeout|200|Object has timed out
            string? firstKnownObjectId = FindFirstKnownObjectIdInEvent(eventRecord, objects);

            (string? compactSourceObjectId, string? compactTargetObjectId) = FindCompactWeaponEventObjects(eventRecord, objects);

            if (eventRecord.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase))
            {
                sourceObjectId ??= compactSourceObjectId;
                targetObjectId ??= compactTargetObjectId ?? firstKnownObjectId;
            }
            else if (eventRecord.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                sourceObjectId ??= firstKnownObjectId;
                targetObjectId ??= firstKnownObjectId;
            }

            TacviewObjectTrack? sourceObject = TryGetObject(objects, sourceObjectId);
            TacviewObjectTrack? targetObject = TryGetObject(objects, targetObjectId);

            TacviewPositionSample? position = ResolveWeaponResultPosition(
                eventRecord,
                sourceObject,
                targetObject);

            return new TacviewWeaponResult
            {
                EventType = eventRecord.EventType,
                TimeSeconds = eventRecord.TimeSeconds,
                AbsoluteTimeUtc = eventRecord.AbsoluteTimeUtc,
                SourceObjectId = sourceObjectId,
                SourceName = sourceObject?.Name ?? sourceObject?.Group,
                TargetObjectId = targetObjectId,
                TargetName = targetObject?.Name ?? targetObject?.Group,
                Outcome = outcome,
                Description = eventRecord.Text,
                Position = position
            };
        }

        private static IReadOnlyList<string> SplitPropertyTokens(string payload)
        {
            List<string> tokens = new();
            StringBuilder current = new();

            bool escaping = false;

            foreach (char ch in payload)
            {
                if (escaping)
                {
                    current.Append(ch);
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (ch == ',')
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString().Trim());
                        current.Clear();
                    }

                    continue;
                }

                current.Append(ch);
            }

            if (escaping)
            {
                current.Append('\\');
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString().Trim());
            }

            return tokens;
        }

        private static double ParseDoubleOrDefault(string value)
        {
            return double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed)
                ? parsed
                : 0;
        }

        private static bool TryParseNullableDouble(string value, out double? parsed)
        {
            parsed = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double concrete))
            {
                return false;
            }

            parsed = concrete;
            return true;
        }

        private static DateTimeOffset? AddSeconds(DateTimeOffset? referenceTimeUtc, double seconds)
        {
            return referenceTimeUtc?.AddSeconds(seconds);
        }

        private static string BuildKml(
            AcmiParseResult parseResult,
            PostBriefingKmlOptions options)
        {
            StringBuilder builder = new();

            builder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
            builder.AppendLine("""<kml xmlns="http://www.opengis.net/kml/2.2">""");
            builder.AppendLine("<Document>");
            builder.AppendLine("<name>DCS Tacview Post Brief</name>");

            AppendPostBriefingStyles(builder);

            Dictionary<string, ObjectDisposition> dispositionsByObjectId =
                BuildObjectDispositionIndex(parseResult.WeaponEngagements);

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
    IReadOnlyList<TacViewWeaponEngagement> weaponEngagements)
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
                    ?? "Unknown Shooter";

                foreach (TacviewWeaponResult result in engagement.Results)
                {
                    string? targetObjectId = result.TargetObjectId;

                    if (string.IsNullOrWhiteSpace(targetObjectId))
                    {
                        continue;
                    }

                    if (!hitsByObjectId.TryGetValue(targetObjectId, out List<ObjectWeaponHit>? hits))
                    {
                        hits = new List<ObjectWeaponHit>();
                        hitsByObjectId[targetObjectId] = hits;
                    }

                    hits.Add(new ObjectWeaponHit(
                        weaponName,
                        engagement.Employment.WeaponObjectId,
                        shooterName,
                        result.AbsoluteTimeUtc,
                        result.TimeSeconds,
                        result.Outcome ?? "Unknown",
                        result.Description ?? string.Empty));

                    if (result.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase))
                    {
                        destroyedResultsByObjectId[targetObjectId] = result;
                    }
                }
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
                        .ToList());
            }

            return dispositions;
        }

        private static (string? SourceObjectId, string? TargetObjectId) FindCompactWeaponEventObjects(
    TacviewEventRecord eventRecord,
    IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            string? sourceObjectId = null;
            string? targetObjectId = null;

            foreach (string part in eventRecord.Parts.Skip(1))
            {
                string candidate = part.Trim();

                if (!objects.TryGetValue(candidate, out TacviewObjectTrack? candidateObject))
                {
                    int colonIndex = candidate.IndexOf(':');

                    if (colonIndex < 0)
                    {
                        continue;
                    }

                    string valueAfterColon = candidate[(colonIndex + 1)..].Trim();

                    if (!objects.TryGetValue(valueAfterColon, out candidateObject))
                    {
                        continue;
                    }

                    candidate = valueAfterColon;
                }

                if (candidateObject.IsWeapon)
                {
                    sourceObjectId ??= candidate;
                    continue;
                }

                targetObjectId ??= candidate;
            }

            return (sourceObjectId, targetObjectId);
        }

        private static string? FindFirstKnownObjectIdInEvent(
    TacviewEventRecord eventRecord,
    IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            foreach (string part in eventRecord.Parts.Skip(1))
            {
                string candidate = part.Trim();

                if (objects.ContainsKey(candidate))
                {
                    return candidate;
                }

                int colonIndex = candidate.IndexOf(':');
                if (colonIndex >= 0)
                {
                    string valueAfterColon = candidate[(colonIndex + 1)..].Trim();

                    if (objects.ContainsKey(valueAfterColon))
                    {
                        return valueAfterColon;
                    }
                }
            }

            return null;
        }

        private static TacviewObjectTrack? TryGetObject(
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

        private static TacviewPositionSample? ResolveWeaponResultPosition(
     TacviewEventRecord eventRecord,
     TacviewObjectTrack? sourceObject,
     TacviewObjectTrack? targetObject)
        {
            if (eventRecord.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase))
            {
                return targetObject?.End
                    ?? sourceObject?.End;
            }

            if (eventRecord.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                return sourceObject?.End
                    ?? targetObject?.End;
            }

            return targetObject?.End
                ?? sourceObject?.End;
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

        private static void AppendPostBriefingStyles(StringBuilder builder)
        {
            builder.AppendLine("""
        <Style id="blueTrackStyle">
            <LineStyle><color>ffff0000</color><width>4</width></LineStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="redTrackStyle">
            <LineStyle><color>ff0000ff</color><width>4</width></LineStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="neutralTrackStyle">
            <LineStyle><color>ffffffff</color><width>4</width></LineStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="blueSamplePointStyle">
            <IconStyle>
                <scale>0.55</scale>
                <color>ffff0000</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon>
            </IconStyle>
            <LabelStyle><scale>0</scale></LabelStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="redSamplePointStyle">
            <IconStyle>
                <scale>0.55</scale>
                <color>ff0000ff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon>
            </IconStyle>
            <LabelStyle><scale>0</scale></LabelStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="neutralSamplePointStyle">
            <IconStyle>
                <scale>0.55</scale>
                <color>ffffffff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon>
            </IconStyle>
            <LabelStyle><scale>0</scale></LabelStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="bluePlaneStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ffff0000</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/airports.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="redPlaneStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ff0000ff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/airports.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="neutralPlaneStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ffffffff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/airports.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="blueShipStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ffff0000</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/marina.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="redShipStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ff0000ff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/marina.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="neutralShipStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ffffffff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/marina.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="blueHeloStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ffff0000</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/heliport.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="redHeloStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ff0000ff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/heliport.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="neutralHeloStartStyle">
            <IconStyle>
                <scale>1.15</scale>
                <color>ffffffff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/heliport.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="blueGroundStartStyle">
            <IconStyle>
                <scale>1.1</scale>
                <color>ffff0000</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/truck.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="redGroundStartStyle">
            <IconStyle>
                <scale>1.1</scale>
                <color>ff0000ff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/truck.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
        <Style id="neutralGroundStartStyle">
            <IconStyle>
                <scale>1.1</scale>
                <color>ffffffff</color>
                <Icon><href>https://maps.google.com/mapfiles/kml/shapes/truck.png</href></Icon>
            </IconStyle>
        </Style>
        """);

            builder.AppendLine("""
            <Style id="blueBullseyeStyle">
                <IconStyle>
                    <scale>1.35</scale>
                    <color>ffff0000</color>
                    <Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon>
                </IconStyle>
                <LabelStyle><scale>1.0</scale></LabelStyle>
            </Style>
            """);

            builder.AppendLine("""
            <Style id="redBullseyeStyle">
                <IconStyle>
                    <scale>1.35</scale>
                    <color>ff0000ff</color>
                    <Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon>
                </IconStyle>
                <LabelStyle><scale>1.0</scale></LabelStyle>
            </Style>
            """);

            builder.AppendLine("""
            <Style id="neutralBullseyeStyle">
                <IconStyle>
                    <scale>1.35</scale>
                    <color>ffffffff</color>
                    <Icon><href>https://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon>
                </IconStyle>
                <LabelStyle><scale>1.0</scale></LabelStyle>
            </Style>
            """);

            builder.AppendLine("""
            <Style id="blueBullseyeRingStyle">
                <LineStyle><color>ffff0000</color><width>2</width></LineStyle>
                <PolyStyle><color>00000000</color></PolyStyle>
            </Style>
            """);

            builder.AppendLine("""
            <Style id="redBullseyeRingStyle">
                <LineStyle><color>ff0000ff</color><width>2</width></LineStyle>
                <PolyStyle><color>00000000</color></PolyStyle>
            </Style>
            """);

            builder.AppendLine("""
            <Style id="neutralBullseyeRingStyle">
                <LineStyle><color>ffffffff</color><width>2</width></LineStyle>
                <PolyStyle><color>00000000</color></PolyStyle>
            </Style>
            """);

            builder.AppendLine("""
<Style id="weaponTrackStyle">
    <LineStyle><color>ff00ffff</color><width>3</width></LineStyle>
</Style>
""");

            builder.AppendLine("""
<Style id="weaponPointStyle">
    <IconStyle>
        <scale>0.75</scale>
        <color>ff00ffff</color>
        <Icon><href>icons/missile.png</href></Icon>
    </IconStyle>
    <LabelStyle><scale>0.75</scale></LabelStyle>
</Style>
""");

            builder.AppendLine("""
<Style id="weaponResultStyle">
    <IconStyle>
        <scale>0.55</scale>
        <color>ff00ffff</color>
        <Icon><href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon>
    </IconStyle>
    <LabelStyle><scale>0</scale></LabelStyle>
</Style>
""");
            builder.AppendLine("""
<Style id="destroyedObjectStyle">
    <IconStyle>
        <scale>0.75</scale>
        <Icon><href>icons/explode.svg</href></Icon>
    </IconStyle>
    <LabelStyle><scale>0</scale></LabelStyle>
</Style>
""");
            builder.AppendLine("""
<Style id="weaponEmploymentBombStyle">
    <IconStyle>
        <scale>0.9</scale>
        <Icon><href>icons/bomb.png</href></Icon>
    </IconStyle>
    <LabelStyle><scale>0.85</scale></LabelStyle>
</Style>
""");

            builder.AppendLine("""
<Style id="weaponEmploymentMissileStyle">
    <IconStyle>
        <scale>0.9</scale>
        <Icon><href>icons/missile.png</href></Icon>
    </IconStyle>
    <LabelStyle><scale>0.85</scale></LabelStyle>
</Style>
""");

            builder.AppendLine("""
<Style id="weaponEmploymentSamStyle">
    <IconStyle>
        <scale>0.9</scale>
        <Icon><href>icons/sam.png</href></Icon>
    </IconStyle>
    <LabelStyle><scale>0.85</scale></LabelStyle>
</Style>

<Style id="weaponEmploymentBulletStyle">
    <IconStyle>
        <scale>0.8</scale>
        <color>ff00ffff</color>
        <Icon><href>https://maps.google.com/mapfiles/kml/shapes/shaded_dot.png</href></Icon>
    </IconStyle>
    <LabelStyle><scale>0.85</scale></LabelStyle>
</Style>
""");
            builder.AppendLine("""
<Style id="blueSamStartStyle">
    <IconStyle>
        <scale>1.1</scale>
        <Icon><href>icons/sam.png</href></Icon>
    </IconStyle>
</Style>
""");

            builder.AppendLine("""
<Style id="redSamStartStyle">
    <IconStyle>
        <scale>1.1</scale>
        <Icon><href>icons/sam.png</href></Icon>
    </IconStyle>
</Style>
""");

            builder.AppendLine("""
<Style id="neutralSamStartStyle">
    <IconStyle>
        <scale>1.1</scale>
        <Icon><href>icons/sam.png</href></Icon>
    </IconStyle>
</Style>
""");

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
                    DistanceMeters = CalculateDistanceMeters(track.End!, result.Position),
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

        private static bool HasNonTimeoutWeaponResult(TacViewWeaponEngagement engagement)
        {
            return engagement.Results.Any(result =>
                !result.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase));
        }

        private static double CalculateDistanceMeters(
            TacviewPositionSample first,
            TacviewPositionSample second)
        {
            const double earthRadiusMeters = 6371000.0;

            double lat1 = DegreesToRadians(first.Latitude);
            double lat2 = DegreesToRadians(second.Latitude);
            double deltaLat = DegreesToRadians(second.Latitude - first.Latitude);
            double deltaLon = DegreesToRadians(second.Longitude - first.Longitude);

            double a =
                Math.Sin(deltaLat / 2.0) * Math.Sin(deltaLat / 2.0)
                + Math.Cos(lat1) * Math.Cos(lat2)
                * Math.Sin(deltaLon / 2.0) * Math.Sin(deltaLon / 2.0);

            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return earthRadiusMeters * c;
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
                if (!dispositionsByObjectId.TryGetValue(track.ObjectId, out ObjectDisposition? disposition)
                    || track.End is null)
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
            TacviewPositionSample? position = track.End;

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
            builder.AppendLine($"Destroyed At: {FormatTime(disposition.DestroyedAtUtc, disposition.DestroyedAtSeconds)}");

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
                .Where(hit => Math.Abs(hit.HitTimeSeconds - disposition.DestroyedAtSeconds) <= 0.25)
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
                ?? "Unknown Shooter";

            string folderName = string.Create(
                CultureInfo.InvariantCulture,
                $"{weaponName} - {shooterName} - {FormatTime(engagement.Employment.Position)}");

            AppendFolderStart(
                builder,
                folderName,
                visible: HasNonTimeoutWeaponResult(engagement));

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
                ?? "Unknown Shooter";

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
                    builder.AppendLine($"Destroyed At: {FormatTime(disposition.DestroyedAtUtc, disposition.DestroyedAtSeconds)}");
                }

                if (!disposition.WasDestroyed && disposition.WeaponHits.Count > 0)
                {
                    ObjectWeaponHit lastHit = disposition.WeaponHits
                        .OrderByDescending(hit => hit.HitTimeSeconds)
                        .First();

                    builder.AppendLine();
                    builder.AppendLine("Damage Evidence:");
                    builder.AppendLine($"Inferred / recorded weapon hits: {disposition.WeaponHits.Count}");
                    builder.AppendLine($"Last recorded hit: {FormatTime(lastHit.HitTimeUtc, lastHit.HitTimeSeconds)}");
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
                        $"- {hit.WeaponName} [{hit.WeaponObjectId}] from {hit.ShooterName} at {FormatTime(hit.HitTimeUtc, hit.HitTimeSeconds)}");
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string GetDisplayName(TacviewObjectTrack track)
        {
            if (!string.IsNullOrWhiteSpace(track.Group))
            {
                return track.Group;
            }

            if (!string.IsNullOrWhiteSpace(track.Name))
            {
                return track.Name;
            }

            return track.ObjectId;
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

        private sealed record TacviewHealthChangeRecord(
            string ObjectId,
            double PreviousHealth,
            double NewHealth,
            double TimeSeconds,
            DateTimeOffset? AbsoluteTimeUtc,
            TacviewPositionSample? Position);

        private sealed record InferredDamageMatch(
            TacviewObjectTrack Weapon,
            TacviewObjectTrack Target,
            TacviewPositionSample TargetPosition,
            double DistanceMeters,
            double DeltaTimeSeconds);

        private sealed record TacviewRemovalRecord(
            string ObjectId,
            double TimeSeconds,
            DateTimeOffset? AbsoluteTimeUtc);

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
            double DestroyedAtSeconds,
            IReadOnlyList<ObjectWeaponHit> WeaponHits);

        private sealed record ObjectWeaponHit(
            string WeaponName,
            string WeaponObjectId,
            string ShooterName,
            DateTimeOffset? HitTimeUtc,
            double HitTimeSeconds,
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
