using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Implements post-briefing processing of Tacview ACMI data, including parsing of zipped ACMI files, inference of weapon results from object removals and health changes, and generation of KML output for visualization.
    /// </summary>
    public sealed class PostBriefingService : IPostBriefingService
    {
        #region Fields
        private readonly IBriefingStylesService _briefingStylesService;
        private readonly WeaponResultInferenceOptions _weaponResultInferenceOptions;

        #endregion Fields

        #region Ctor

        public PostBriefingService(
            IBriefingStylesService? briefingStylesService = null,
            WeaponResultInferenceOptions? weaponResultInferenceOptions = null)
        {
            _briefingStylesService = briefingStylesService ?? new BriefingStylesService();
            _weaponResultInferenceOptions = weaponResultInferenceOptions ?? new WeaponResultInferenceOptions();
        }

        #endregion Ctor

        #region IPostBriefing Service Implementation

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

            AcmiParseResult parseResult = BuildPostBriefingParseResult(
                acmiData,
                _weaponResultInferenceOptions);

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

        #endregion IPostBriefing Service Implementation

        #region Private Methods

        private AcmiParseResult BuildPostBriefingParseResult(
            TacviewAcmiParseData acmiData,
            WeaponResultInferenceOptions inferenceOptions)
        {
            List<TacviewObjectTrack> groupTracks = acmiData.Objects.Values
                .Where(o => !ShouldSuppressFromObjectTracks(o))
                .Where(o => o.Samples.Count > 0)
                .OrderBy(o => o.Group ?? o.Name ?? o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<TacviewWeaponEmployment> weaponEmployments = acmiData.Objects.Values
                .Where(o => o.IsWeapon)
                .Where(IsRelevantWeaponObject)
                .Where(o => o.Start is not null)
                .Select(o => CreateWeaponEmployment(o, acmiData.Objects))
                .ToList();

            List<TacviewWeaponResult> explicitWeaponResults = acmiData.Events
                .Where(IsWeaponResultEventType)
                .Select(e => CreateWeaponResult(e, acmiData.Objects))
                .ToList();

            List<TacviewObjectTrack> weaponTracks = acmiData.Objects.Values
                .Where(IsRelevantWeaponObject)
                .Where(o => o.Samples.Count > 0)
                .OrderBy(o => o.Name ?? o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<TacviewWeaponResult> inferredDamageResults = new();

            inferredDamageResults.AddRange(
                CreateWeaponResultsFromHealthChanges(
                    acmiData.HealthChanges,
                    acmiData.Objects,
                    weaponTracks,
                    inferenceOptions));

            HashSet<string> weaponIdsWithNonTimeoutResults =
                BuildWeaponIdsWithNonTimeoutResults(
                    explicitWeaponResults.Concat(inferredDamageResults),
                    weaponTracks);

            inferredDamageResults.AddRange(
                CreateWeaponResultsFromUnpairedWeaponRemovals(
                    acmiData.Removals,
                    acmiData.Objects,
                    weaponTracks,
                    weaponIdsWithNonTimeoutResults,
                    inferenceOptions));

            weaponIdsWithNonTimeoutResults =
                BuildWeaponIdsWithNonTimeoutResults(
                    explicitWeaponResults.Concat(inferredDamageResults),
                    weaponTracks);

            List<TacviewWeaponResult> weaponResults = explicitWeaponResults
                .Concat(inferredDamageResults)
                .ToList();

            weaponResults.AddRange(
                CreateWeaponResultsFromRemovals(
                    acmiData.Removals,
                    acmiData.Objects,
                    weaponTracks,
                    weaponIdsWithNonTimeoutResults,
                    inferenceOptions));

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
            IReadOnlySet<string> weaponIdsWithNonTimeoutResults,
            WeaponResultInferenceOptions inferenceOptions)
        {
            var results = new List<TacviewWeaponResult>();

            Dictionary<string, TacviewObjectTrack> weaponTracksById = weaponTracks
                .ToDictionary(w => w.ObjectId, StringComparer.OrdinalIgnoreCase);

            HashSet<string> weaponIdsWithResults = new(
                weaponIdsWithNonTimeoutResults,
                StringComparer.OrdinalIgnoreCase);

            results.AddRange(
                CreateWeaponVsWeaponRemovalResults(
                    removals,
                    objects,
                    weaponTracksById,
                    weaponIdsWithResults,
                    inferenceOptions));

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
                    .Where(r => Math.Abs(r.TimeSeconds - weaponRemoval.TimeSeconds) <= inferenceOptions.SameTimeRemovalWindowSeconds)
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

                    weaponIdsWithResults.Add(weapon.ObjectId);

                    continue;
                }

                if (weaponIdsWithResults.Contains(weapon.ObjectId))
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

        private static IReadOnlyList<TacviewWeaponResult> CreateWeaponVsWeaponRemovalResults(
            IReadOnlyList<TacviewRemovalRecord> removals,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            IReadOnlyDictionary<string, TacviewObjectTrack> weaponTracksById,
            ISet<string> weaponIdsWithResults,
            WeaponResultInferenceOptions inferenceOptions)
        {
            var results = new List<TacviewWeaponResult>();
            HashSet<string> consumedWeaponIds = new(StringComparer.OrdinalIgnoreCase);

            List<TacviewRemovalRecord> weaponRemovals = removals
                .Where(removal => weaponTracksById.ContainsKey(removal.ObjectId))
                .OrderBy(removal => removal.TimeSeconds)
                .ThenBy(removal => removal.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (TacviewRemovalRecord firstRemoval in weaponRemovals)
            {
                if (consumedWeaponIds.Contains(firstRemoval.ObjectId)
                    || weaponIdsWithResults.Contains(firstRemoval.ObjectId))
                {
                    continue;
                }

                TacviewObjectTrack firstWeapon = weaponTracksById[firstRemoval.ObjectId];

                TacviewPositionSample? firstPosition =
                    firstWeapon.End
                    ?? FindSampleClosestToTime(firstWeapon.Samples, firstRemoval.TimeSeconds);

                if (firstPosition is null)
                {
                    continue;
                }

                WeaponInterceptMatch? bestMatch = null;

                foreach (TacviewRemovalRecord secondRemoval in weaponRemovals)
                {
                    if (secondRemoval.ObjectId.Equals(firstRemoval.ObjectId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (consumedWeaponIds.Contains(secondRemoval.ObjectId)
                        || weaponIdsWithResults.Contains(secondRemoval.ObjectId))
                    {
                        continue;
                    }

                    double timeDifference = Math.Abs(secondRemoval.TimeSeconds - firstRemoval.TimeSeconds);

                    if (timeDifference > inferenceOptions.DefensivePairMaxTimeDifferenceSeconds)
                    {
                        continue;
                    }

                    TacviewObjectTrack secondWeapon = weaponTracksById[secondRemoval.ObjectId];

                    if (!TryResolveInterceptorAndInterceptedWeapon(
                            firstWeapon,
                            secondWeapon,
                            objects,
                            out TacviewObjectTrack? interceptor,
                            out TacviewObjectTrack? interceptedWeapon))
                    {
                        continue;
                    }

                    TacviewPositionSample? secondPosition =
                        secondWeapon.End
                        ?? FindSampleClosestToTime(secondWeapon.Samples, secondRemoval.TimeSeconds);

                    if (secondPosition is null)
                    {
                        continue;
                    }

                    double distanceMeters = TacviewCombatClassifier.CalculateDistance3dMeters(
                        firstPosition,
                        secondPosition);

                    if (distanceMeters > inferenceOptions.DefensivePairMaxDistanceMeters)
                    {
                        continue;
                    }

                    var match = new WeaponInterceptMatch(
                        interceptor,
                        interceptedWeapon,
                        firstRemoval,
                        secondRemoval,
                        distanceMeters,
                        timeDifference);

                    if (bestMatch is null
                        || match.DistanceMeters < bestMatch.DistanceMeters
                        || (Math.Abs(match.DistanceMeters - bestMatch.DistanceMeters) < 0.001
                            && match.TimeDifferenceSeconds < bestMatch.TimeDifferenceSeconds))
                    {
                        bestMatch = match;
                    }
                }

                if (bestMatch is null)
                {
                    continue;
                }

                TacviewRemovalRecord sourceRemoval =
                    bestMatch.Interceptor.ObjectId.Equals(bestMatch.FirstRemoval.ObjectId, StringComparison.OrdinalIgnoreCase)
                        ? bestMatch.FirstRemoval
                        : bestMatch.SecondRemoval;

                results.Add(new TacviewWeaponResult
                {
                    EventType = "Destroyed",
                    TimeSeconds = sourceRemoval.TimeSeconds,
                    AbsoluteTimeUtc = sourceRemoval.AbsoluteTimeUtc,
                    SourceObjectId = bestMatch.Interceptor.ObjectId,
                    SourceName = bestMatch.Interceptor.Name,
                    TargetObjectId = bestMatch.InterceptedWeapon.ObjectId,
                    TargetName = bestMatch.InterceptedWeapon.Name,
                    Outcome = "Weapon intercepted opposing strike weapon",
                    Description = string.Create(
                        CultureInfo.InvariantCulture,
                        $"Synthesized weapon-vs-weapon intercept from Tacview removal records: -{bestMatch.Interceptor.ObjectId} and -{bestMatch.InterceptedWeapon.ObjectId}; 3D distance {bestMatch.DistanceMeters:F0} m"),
                    Position = bestMatch.InterceptedWeapon.End ?? bestMatch.Interceptor.End
                });

                consumedWeaponIds.Add(bestMatch.Interceptor.ObjectId);
                consumedWeaponIds.Add(bestMatch.InterceptedWeapon.ObjectId);
                weaponIdsWithResults.Add(bestMatch.Interceptor.ObjectId);
                weaponIdsWithResults.Add(bestMatch.InterceptedWeapon.ObjectId);
            }

            return results;
        }

        private static bool TryResolveInterceptorAndInterceptedWeapon(
            TacviewObjectTrack firstWeapon,
            TacviewObjectTrack secondWeapon,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            out TacviewObjectTrack? interceptor,
            out TacviewObjectTrack? interceptedWeapon)
        {
            TacviewObjectTrack? firstLauncher = ResolveWeaponShooter(firstWeapon, objects);
            TacviewObjectTrack? secondLauncher = ResolveWeaponShooter(secondWeapon, objects);

            bool firstIsInterceptor =
                TacviewCombatClassifier.IsDefensiveInterceptor(firstWeapon, firstLauncher);

            bool secondIsInterceptor =
                TacviewCombatClassifier.IsDefensiveInterceptor(secondWeapon, secondLauncher);

            bool firstIsStrike =
                TacviewCombatClassifier.IsOffensiveStrikeWeapon(firstWeapon, firstLauncher);

            bool secondIsStrike =
                TacviewCombatClassifier.IsOffensiveStrikeWeapon(secondWeapon, secondLauncher);

            if (firstIsInterceptor && secondIsStrike && !secondIsInterceptor)
            {
                interceptor = firstWeapon;
                interceptedWeapon = secondWeapon;
                return true;
            }

            if (secondIsInterceptor && firstIsStrike && !firstIsInterceptor)
            {
                interceptor = secondWeapon;
                interceptedWeapon = firstWeapon;
                return true;
            }

            interceptor = null;
            interceptedWeapon = null;
            return false;
        }

        private static IReadOnlyList<TacviewWeaponResult> CreateWeaponResultsFromHealthChanges(
            IReadOnlyList<TacviewHealthChangeRecord> healthChanges,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            IReadOnlyList<TacviewObjectTrack> weaponTracks,
            WeaponResultInferenceOptions inferenceOptions)
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
                    weaponTracks,
                    inferenceOptions);

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

        private static IReadOnlyList<TacviewWeaponResult> CreateWeaponResultsFromUnpairedWeaponRemovals(
            IReadOnlyList<TacviewRemovalRecord> removals,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            IReadOnlyList<TacviewObjectTrack> weaponTracks,
            IReadOnlySet<string> weaponIdsWithNonTimeoutResults,
            WeaponResultInferenceOptions inferenceOptions)
        {
            if (!inferenceOptions.EnableTerminalProximityDamageInference
                && !inferenceOptions.EnableTerminalProximityNearMissReporting)
            {
                return Array.Empty<TacviewWeaponResult>();
            }

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

                if (HasSynchronizedTargetRemoval(
                        weaponRemoval,
                        removals,
                        objects,
                        inferenceOptions))
                {
                    continue;
                }

                InferredDamageMatch? match = FindBestUnpairedRemovalDamageTarget(
                    weapon,
                    weaponRemoval,
                    removals,
                    objects.Values,
                    weaponTracksById,
                    inferenceOptions);

                if (match is null)
                {
                    continue;
                }

                bool classifyAsDamage = inferenceOptions.EnableTerminalProximityDamageInference && ShouldPromoteTerminalProximityToDamage(match.Target);
                results.Add(new TacviewWeaponResult
                {
                    EventType = classifyAsDamage
                        ? "Damage"
                        : "NearMiss",

                    TimeSeconds = weaponRemoval.TimeSeconds,
                    AbsoluteTimeUtc = weaponRemoval.AbsoluteTimeUtc,
                    SourceObjectId = weapon.ObjectId,
                    SourceName = weapon.Name,
                    TargetObjectId = match.Target.ObjectId,
                    TargetName = GetDisplayName(match.Target),

                    Outcome = classifyAsDamage
                        ? "Inferred from unpaired opposing weapon removal near target"
                        : "Terminal proximity only; not classified as damage",

                    Description = classifyAsDamage
                        ? string.Create(
                            CultureInfo.InvariantCulture,
                            $"Weapon {weapon.ObjectId} removed near opposing target {match.Target.ObjectId}; distance {match.DistanceMeters:F0} m; no paired defensive weapon removal found")
                        : string.Create(
                            CultureInfo.InvariantCulture,
                            $"Weapon {weapon.ObjectId} ended near opposing target {match.Target.ObjectId}; distance {match.DistanceMeters:F0} m; recorded as near miss because terminal proximity alone is not damage"),

                    Position = match.TargetPosition
                });

            }

            return results;
        }

        private static bool ShouldPromoteTerminalProximityToDamage(TacviewObjectTrack target)
        {
            return TacviewCombatClassifier.GetTargetDomain(target) == TacviewTargetDomain.Sea;
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
            IReadOnlyList<TacviewObjectTrack> weaponTracks,
            WeaponResultInferenceOptions inferenceOptions)
        {
            TacviewPositionSample? targetPosition =
                healthChange.Position
                ?? target.End
                ?? FindSampleClosestToTime(target.Samples, healthChange.TimeSeconds);

            if (targetPosition is null)
            {
                return null;
            }

            return weaponTracks
                .Where(weapon => weapon.End is not null)
                .Where(weapon => IsPotentialDamageTargetForWeapon(weapon, target))
                .Select(weapon => new InferredDamageMatch(
                    weapon,
                    target,
                    targetPosition,
                    TacviewCombatClassifier.CalculateDistance3dMeters(weapon.End!, targetPosition),
                    healthChange.TimeSeconds - weapon.End!.TimeSeconds))
                .Where(match => match.DistanceMeters <= inferenceOptions.HealthDropMaxDamageDistanceMeters)
                .Where(match => match.DeltaTimeSeconds >= -inferenceOptions.HealthDropMaxWeaponTimeAfterDamageSeconds
                    && match.DeltaTimeSeconds <= inferenceOptions.HealthDropMaxWeaponTimeBeforeDamageSeconds)
                .OrderBy(match => match.DistanceMeters)
                .ThenBy(match => Math.Abs(match.DeltaTimeSeconds))
                .FirstOrDefault();
        }

        private static InferredDamageMatch? FindBestUnpairedRemovalDamageTarget(
            TacviewObjectTrack weapon,
            TacviewRemovalRecord weaponRemoval,
            IReadOnlyList<TacviewRemovalRecord> removals,
            IEnumerable<TacviewObjectTrack> objects,
            IReadOnlyDictionary<string, TacviewObjectTrack> weaponTracksById,
            WeaponResultInferenceOptions inferenceOptions)
        {
            TacviewPositionSample? weaponPosition =
                weapon.End
                ?? FindSampleClosestToTime(weapon.Samples, weaponRemoval.TimeSeconds);

            if (weaponPosition is null)
            {
                return null;
            }

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

                if (targetTimeDifference > inferenceOptions.UnpairedRemovalMaxTargetSampleTimeDifferenceSeconds)
                {
                    continue;
                }

                double distanceMeters = TacviewCombatClassifier.CalculateDistance3dMeters(weaponPosition, targetPosition);

                if (distanceMeters > inferenceOptions.UnpairedRemovalMaxInferredDamageDistanceMeters)
                {
                    continue;
                }

                if (HasNearbyDefensiveWeaponRemoval(
                     weapon,
                     weaponPosition,
                     weaponRemoval,
                     target,
                     removals,
                     objects,
                     weaponTracksById,
                     inferenceOptions))
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
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            WeaponResultInferenceOptions inferenceOptions)
        {
            return removals
                .Where(r => Math.Abs(r.TimeSeconds - weaponRemoval.TimeSeconds) <= inferenceOptions.SameTimeRemovalWindowSeconds)
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
            IEnumerable<TacviewObjectTrack> objects,
            IReadOnlyDictionary<string, TacviewObjectTrack> weaponTracksById,
            WeaponResultInferenceOptions inferenceOptions)
        {
            Dictionary<string, TacviewObjectTrack> objectsById = objects
                .ToDictionary(o => o.ObjectId, StringComparer.OrdinalIgnoreCase);

            TacviewObjectTrack? inboundLauncher = ResolveWeaponShooter(weapon, objectsById);

            if (!TacviewCombatClassifier.IsOffensiveStrikeWeapon(weapon, inboundLauncher))
            {
                return false;
            }

            foreach (TacviewRemovalRecord otherRemoval in removals)
            {
                if (otherRemoval.ObjectId.Equals(weaponRemoval.ObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double timeDifferenceSeconds = Math.Abs(otherRemoval.TimeSeconds - weaponRemoval.TimeSeconds);

                if (timeDifferenceSeconds > inferenceOptions.DefensivePairMaxTimeDifferenceSeconds)
                {
                    continue;
                }

                if (!weaponTracksById.TryGetValue(otherRemoval.ObjectId, out TacviewObjectTrack? otherWeapon))
                {
                    continue;
                }

                TacviewObjectTrack? defensiveLauncher = ResolveWeaponShooter(otherWeapon, objectsById);

                if (!TacviewCombatClassifier.IsDefensiveInterceptor(otherWeapon, defensiveLauncher))
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

                double distanceMeters = TacviewCombatClassifier.CalculateDistance3dMeters(
                    weaponPosition,
                    otherWeaponPosition);

                if (distanceMeters <= inferenceOptions.DefensivePairMaxDistanceMeters)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsObjectEffectWeaponResultType(string eventType)
        {
            return eventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPotentialDamageTargetForWeapon(
            TacviewObjectTrack weapon,
            TacviewObjectTrack target)
        {
            if (target.IsWeapon)
            {
                return false;
            }

            if (IsSuppressedResultObject(target))
            {
                return false;
            }

            TacviewTargetDomain targetDomain = TacviewCombatClassifier.GetTargetDomain(target);

            return targetDomain is
                TacviewTargetDomain.Air
                or TacviewTargetDomain.Sea
                or TacviewTargetDomain.Ground
                or TacviewTargetDomain.Static;
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
                    if (!IsObjectEffectWeaponResultType(result.EventType))
                    {
                        continue;
                    }

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
                ?? "Unknown Shooter";

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

            if (result.EventType.Equals("NearMiss", StringComparison.OrdinalIgnoreCase))
            {
                string targetName = string.IsNullOrWhiteSpace(result.TargetName)
                    ? "Unknown Target"
                    : result.TargetName;

                return $"Near Miss - {targetName}";
            }

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
                    builder.AppendLine($"Destroyed At: {FormatTime(disposition.DestroyedAtUtc, disposition.DestroyedAtSeconds ?? 0.00)}");
                }

                if (!disposition.WasDestroyed && disposition.WeaponHits.Count > 0)
                {
                    ObjectWeaponHit lastHit = disposition.WeaponHits
                        .OrderByDescending(hit => hit.HitTimeSeconds)
                        .First();

                    builder.AppendLine();
                    builder.AppendLine("Damage Evidence:");
                    builder.AppendLine($"Inferred / recorded weapon hits: {disposition.WeaponHits.Count}");
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
                        $"- {hit.WeaponName} [{hit.WeaponObjectId}] from {hit.ShooterName} at {FormatTime(hit.HitTimeUtc, hit.HitTimeSeconds ?? 0)}");
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

        private sealed record InferredDamageMatch(
            TacviewObjectTrack Weapon,
            TacviewObjectTrack Target,
            TacviewPositionSample TargetPosition,
            double DistanceMeters,
            double DeltaTimeSeconds);

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

        private sealed record WeaponInterceptMatch(
            TacviewObjectTrack Interceptor,
            TacviewObjectTrack InterceptedWeapon,
            TacviewRemovalRecord FirstRemoval,
            TacviewRemovalRecord SecondRemoval,
            double DistanceMeters,
            double TimeDifferenceSeconds);

        #endregion Private Methods
    }

    internal static class StringBuilderExtensions
    {
        public static void AppendElement(
            this StringBuilder builder,
            string elementName,
            string value)
        {
            builder.Append('<');
            builder.Append(elementName);
            builder.Append('>');
            builder.Append(System.Security.SecurityElement.Escape(value) ?? string.Empty);
            builder.Append("</");
            builder.Append(elementName);
            builder.AppendLine(">");
        }
    }
}
