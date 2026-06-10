using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class PostBriefingWeaponEventResultFactory
    {
        public static bool IsWeaponResultEventType(TacviewEventRecord eventRecord)
        {
            return eventRecord.EventType.Equals("Destroyed", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Timeout", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Hit", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damage", StringComparison.OrdinalIgnoreCase)
                || eventRecord.EventType.Equals("Damaged", StringComparison.OrdinalIgnoreCase);
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

            string? firstKnownObjectId = FindFirstKnownObjectIdInEvent(eventRecord, objects);

            (string? compactSourceObjectId, string? compactTargetObjectId) =
                FindCompactWeaponEventObjects(eventRecord, objects);

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

    }
}


