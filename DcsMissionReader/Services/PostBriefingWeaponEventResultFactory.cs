using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class PostBriefingWeaponEventResultFactory
    {
        private static readonly string[] ObjectEffectPhrases =
        [
            " has destroyed ",
            " has hit ",
            " has damaged "
        ];

        public static bool IsWeaponResultEventType(TacviewEventRecord eventRecord)
        {
            return eventRecord.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase)
                || TryGetObjectEffectEventTypeFromText(eventRecord.Text, out _);
        }

        public static TacviewWeaponResult CreateWeaponResult(
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

            string eventType = ResolveWeaponResultEventType(eventRecord);

            string? firstKnownObjectId = FindFirstKnownObjectIdInEvent(eventRecord, objects);

            (string? compactSourceObjectId, string? compactTargetObjectId) =
                FindCompactWeaponEventObjects(eventRecord, objects);

            if (IsObjectEffectEventType(eventType))
            {
                sourceObjectId ??= compactSourceObjectId;
                targetObjectId ??= compactTargetObjectId ?? firstKnownObjectId;

                if (sourceObjectId is null || targetObjectId is null)
                {
                    (string? textSourceObjectId, string? textTargetObjectId) =
                        FindNaturalLanguageWeaponEventObjects(eventRecord, objects);

                    sourceObjectId ??= textSourceObjectId;
                    targetObjectId ??= textTargetObjectId;
                }
            }
            else if (eventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                sourceObjectId ??= firstKnownObjectId;
                targetObjectId ??= firstKnownObjectId;
            }

            TacviewObjectTrack? sourceObject = TryGetObject(objects, sourceObjectId);
            TacviewObjectTrack? targetObject = TryGetObject(objects, targetObjectId);

            TacviewPositionSample? position = ResolveWeaponResultPosition(
                eventType,
                sourceObject,
                targetObject);

            return new TacviewWeaponResult
            {
                EventType = eventType,
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

        private static string ResolveWeaponResultEventType(TacviewEventRecord eventRecord)
        {
            if (eventRecord.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase))
            {
                return eventRecord.EventType;
            }

            return TryGetObjectEffectEventTypeFromText(eventRecord.Text, out string? eventType)
                ? eventType
                : eventRecord.EventType;
        }

        private static bool IsObjectEffectEventType(string eventType)
        {
            return eventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetObjectEffectEventTypeFromText(
            string? eventText,
            out string eventType)
        {
            eventType = string.Empty;

            if (string.IsNullOrWhiteSpace(eventText))
            {
                return false;
            }

            if (eventText.Contains(" has destroyed ", StringComparison.OrdinalIgnoreCase))
            {
                eventType = "Destroyed";
                return true;
            }

            if (eventText.Contains(" has hit ", StringComparison.OrdinalIgnoreCase))
            {
                eventType = "Hit";
                return true;
            }

            if (eventText.Contains(" has damaged ", StringComparison.OrdinalIgnoreCase))
            {
                eventType = "Damaged";
                return true;
            }

            return false;
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

        private static (string? SourceObjectId, string? TargetObjectId) FindNaturalLanguageWeaponEventObjects(
            TacviewEventRecord eventRecord,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            if (string.IsNullOrWhiteSpace(eventRecord.Text))
            {
                return (null, null);
            }

            string eventText = eventRecord.Text;

            foreach (string phrase in ObjectEffectPhrases)
            {
                int phraseIndex = eventText.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);

                if (phraseIndex < 0)
                {
                    continue;
                }

                string sourceText = eventText[..phraseIndex].Trim();
                string targetText = eventText[(phraseIndex + phrase.Length)..].Trim();

                string? sourceObjectId = FindBestMatchingObjectId(
                    sourceText,
                    objects,
                    requireWeapon: true,
                    eventRecord.TimeSeconds);

                string? targetObjectId = FindBestMatchingObjectId(
                    targetText,
                    objects,
                    requireWeapon: false,
                    eventRecord.TimeSeconds);

                return (sourceObjectId, targetObjectId);
            }

            return (null, null);
        }

        private static string? FindBestMatchingObjectId(
            string text,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects,
            bool requireWeapon,
            double eventTimeSeconds)
        {
            return objects
                .Where(pair => pair.Value.IsWeapon == requireWeapon)
                .Select(pair => new
                {
                    ObjectId = pair.Key,
                    Track = pair.Value,
                    Score = GetTextMatchScore(text, pair.Key, pair.Value),
                    TimeDistance = GetEndTimeDistance(pair.Value, eventTimeSeconds)
                })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.TimeDistance)
                .ThenBy(candidate => candidate.ObjectId, StringComparer.OrdinalIgnoreCase)
                .Select(candidate => candidate.ObjectId)
                .FirstOrDefault();
        }

        private static int GetTextMatchScore(
            string text,
            string objectId,
            TacviewObjectTrack track)
        {
            int score = 0;

            score = Math.Max(score, GetTokenMatchScore(text, objectId, 100));
            score = Math.Max(score, GetTokenMatchScore(text, track.Name, 80));
            score = Math.Max(score, GetTokenMatchScore(text, track.Pilot, 70));
            score = Math.Max(score, GetTokenMatchScore(text, track.Group, 60));

            string displayName = TacviewObjectDisplayName.GetDisplayName(track);

            score = Math.Max(score, GetTokenMatchScore(text, displayName, 90));

            if (!string.IsNullOrWhiteSpace(track.Name)
                && !string.IsNullOrWhiteSpace(track.Pilot))
            {
                score = Math.Max(
                    score,
                    GetTokenMatchScore(text, $"{track.Name} {track.Pilot}", 75));
            }

            if (!string.IsNullOrWhiteSpace(track.Group)
                && !string.IsNullOrWhiteSpace(track.Pilot))
            {
                score = Math.Max(
                    score,
                    GetTokenMatchScore(text, $"{track.Group} {track.Pilot}", 75));
            }

            return score;
        }

        private static int GetTokenMatchScore(
            string text,
            string? token,
            int score)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return 0;
            }

            return text.Contains(token, StringComparison.OrdinalIgnoreCase)
                ? score
                : 0;
        }

        private static double GetEndTimeDistance(
            TacviewObjectTrack track,
            double eventTimeSeconds)
        {
            return track.End is null
                ? double.MaxValue
                : Math.Abs(track.End.TimeSeconds - eventTimeSeconds);
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
            string eventType,
            TacviewObjectTrack? sourceObject,
            TacviewObjectTrack? targetObject)
        {
            if (IsObjectEffectEventType(eventType))
            {
                return targetObject?.End
                    ?? sourceObject?.End;
            }

            if (eventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                return sourceObject?.End
                    ?? targetObject?.End;
            }

            return targetObject?.End
                ?? sourceObject?.End;
        }
    }
}
