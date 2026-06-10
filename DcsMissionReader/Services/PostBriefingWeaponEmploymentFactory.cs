using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class PostBriefingWeaponEmploymentFactory
    {
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

                double distanceMeters = TacviewCombatClassifier.CalculateHorizontalDistanceMeters(
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

