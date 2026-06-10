using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class PostBriefingWeaponEmploymentFactory
    {
        private const double MaxFallbackShooterDistanceMeters = 2_000.0;
        private const double MaxTimeDifferenceSeconds = 5.0;

        private const double MaxReplacementShooterDistanceMeters = 500.0;
        private const double MinClearlyBadParentDistanceMeters = 2_000.0;

        public static TacviewWeaponEmployment CreateWeaponEmployment(
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

        public static TacviewObjectTrack? ResolveWeaponShooter(
            TacviewObjectTrack weapon,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            TacviewObjectTrack? parentCandidate = ResolveParentCandidate(weapon, objects);

            if (parentCandidate is not null)
            {
                return ResolveParentOrBetterNearbyShooter(
                    weapon,
                    parentCandidate,
                    objects.Values);
            }

            return FindClosestLikelyShooterAtLaunch(
                weapon,
                objects.Values,
                MaxFallbackShooterDistanceMeters);
        }

        private static TacviewObjectTrack? ResolveParentCandidate(
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

        private static TacviewObjectTrack ResolveParentOrBetterNearbyShooter(
            TacviewObjectTrack weapon,
            TacviewObjectTrack parentCandidate,
            IEnumerable<TacviewObjectTrack> objects)
        {
            double? parentDistanceMeters = TryCalculateDistanceFromWeaponAtLaunch(
                weapon,
                parentCandidate);

            if (parentDistanceMeters is not null
                && parentDistanceMeters.Value <= MinClearlyBadParentDistanceMeters)
            {
                return parentCandidate;
            }

            TacviewObjectTrack? nearbyCandidate = FindClosestLikelyShooterAtLaunch(
                weapon,
                objects,
                MaxReplacementShooterDistanceMeters);

            if (nearbyCandidate is null)
            {
                return parentCandidate;
            }

            if (nearbyCandidate.ObjectId.Equals(parentCandidate.ObjectId, StringComparison.OrdinalIgnoreCase))
            {
                return parentCandidate;
            }

            double? nearbyDistanceMeters = TryCalculateDistanceFromWeaponAtLaunch(
                weapon,
                nearbyCandidate);

            if (nearbyDistanceMeters is null)
            {
                return parentCandidate;
            }

            bool parentIsClearlyBad =
                parentDistanceMeters is null
                || parentDistanceMeters.Value > MinClearlyBadParentDistanceMeters;

            if (parentIsClearlyBad
                && nearbyDistanceMeters.Value <= MaxReplacementShooterDistanceMeters)
            {
                return nearbyCandidate;
            }

            return parentCandidate;
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
            IEnumerable<TacviewObjectTrack> objects,
            double maxShooterDistanceMeters)
        {
            if (weapon.Start is null)
            {
                return null;
            }

            TacviewObjectTrack? bestObject = null;
            double bestDistanceMeters = double.MaxValue;

            foreach (TacviewObjectTrack candidate in objects)
            {
                double? distanceMeters = TryCalculateDistanceFromWeaponAtLaunch(
                    weapon,
                    candidate);

                if (distanceMeters is null)
                {
                    continue;
                }

                if (distanceMeters.Value > maxShooterDistanceMeters)
                {
                    continue;
                }

                if (distanceMeters.Value < bestDistanceMeters)
                {
                    bestDistanceMeters = distanceMeters.Value;
                    bestObject = candidate;
                }
            }

            return bestObject;
        }

        private static double? TryCalculateDistanceFromWeaponAtLaunch(
            TacviewObjectTrack weapon,
            TacviewObjectTrack candidate)
        {
            if (weapon.Start is null)
            {
                return null;
            }

            if (candidate.ObjectId.Equals(weapon.ObjectId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (candidate.IsWeapon)
            {
                return null;
            }

            if (candidate.Samples.Count == 0)
            {
                return null;
            }

            TacviewPositionSample? candidateSample =
                FindSampleClosestToTime(candidate.Samples, weapon.Start.TimeSeconds);

            if (candidateSample is null)
            {
                return null;
            }

            double timeDifference = Math.Abs(candidateSample.TimeSeconds - weapon.Start.TimeSeconds);

            if (timeDifference > MaxTimeDifferenceSeconds)
            {
                return null;
            }

            return TacviewCombatClassifier.CalculateDistance3dMeters(
                weapon.Start,
                candidateSample);
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
    }
}
