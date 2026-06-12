using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class TacviewWeaponLaunchDetector
    {
        private const double DefaultMaxLauncherDistanceMeters = 250.0;

        public static IReadOnlyList<TacviewWeaponLaunch> Detect(
            TacviewLifecycleReplay replay)
        {
            ArgumentNullException.ThrowIfNull(replay);

            List<TacviewWeaponLaunch> launches = new();

            foreach (TacviewWeaponBirth birth in replay.WeaponBirths
                .OrderBy(b => b.TimeSeconds)
                .ThenBy(b => b.WeaponObjectId, StringComparer.OrdinalIgnoreCase))
            {
                if (!replay.Objects.TryGetValue(
                        birth.WeaponObjectId,
                        out TacviewLifecycleObject? weapon))
                {
                    continue;
                }

                TacviewLifecycleObject? launcher = FindNearestLauncher(
                    replay,
                    weapon,
                    birth,
                    out double? launcherDistanceMeters);

                launches.Add(new TacviewWeaponLaunch
                {
                    WeaponObjectId = weapon.ObjectId,
                    WeaponName = weapon.Name,
                    WeaponType = weapon.Type,
                    WeaponCoalition = weapon.Coalition,
                    WeaponCountry = weapon.Country,
                    LaunchTimeSeconds = birth.TimeSeconds,
                    LaunchSample = birth.BirthSample ?? weapon.Start,

                    LauncherObjectId = launcher?.ObjectId,
                    LauncherName = launcher?.Name,
                    LauncherPilot = launcher?.Pilot,
                    LauncherGroup = launcher?.Group,
                    LauncherType = launcher?.Type,
                    LauncherCoalition = launcher?.Coalition,
                    LauncherCountry = launcher?.Country,
                    LauncherDistanceMeters = launcherDistanceMeters,

                    CorrelationMethod = launcher is null
                        ? TacviewCorrelationMethod.Unknown
                        : TacviewCorrelationMethod.BirthProximity,

                    Confidence = launcher is null
                        ? TacviewCorrelationConfidence.Unknown
                        : GetLauncherConfidence(launcherDistanceMeters)
                });
            }

            return launches;
        }

        private static TacviewLifecycleObject? FindNearestLauncher(
            TacviewLifecycleReplay replay,
            TacviewLifecycleObject weapon,
            TacviewWeaponBirth birth,
            out double? launcherDistanceMeters)
        {
            launcherDistanceMeters = null;

            TacviewLifecycleSample? weaponBirthSample = birth.BirthSample ?? weapon.Start;

            if (weaponBirthSample?.LocalX is null || weaponBirthSample.LocalY is null)
            {
                return null;
            }

            TacviewLifecycleObject? nearest = null;
            double nearestDistance = double.MaxValue;

            foreach (TacviewLifecycleObject candidate in replay.Objects.Values)
            {
                if (candidate.ObjectId.Equals(weapon.ObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (candidate.IsWeapon)
                {
                    continue;
                }

                if (!IsActiveAt(candidate, birth.TimeSeconds))
                {
                    continue;
                }

                if (!SameCoalition(candidate, weapon))
                {
                    continue;
                }

                TacviewLifecycleSample? candidateSample =
                    GetNearestSampleNearTime(candidate, birth.TimeSeconds);

                if (candidateSample?.LocalX is null || candidateSample.LocalY is null)
                {
                    continue;
                }

                double distance = DistanceMeters(
                    weaponBirthSample,
                    candidateSample);

                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            if (nearest is null || nearestDistance > DefaultMaxLauncherDistanceMeters)
            {
                return null;
            }

            launcherDistanceMeters = nearestDistance;
            return nearest;
        }

        private static bool IsActiveAt(
            TacviewLifecycleObject candidate,
            double timeSeconds)
        {
            if (candidate.FirstSeenSeconds is not null
                && candidate.FirstSeenSeconds.Value - timeSeconds > 0.001)
            {
                return false;
            }

            if (candidate.RemovedSeconds is not null
                && timeSeconds - candidate.RemovedSeconds.Value > 0.001)
            {
                return false;
            }

            return true;
        }

        private static bool SameCoalition(
            TacviewLifecycleObject left,
            TacviewLifecycleObject right)
        {
            if (string.IsNullOrWhiteSpace(left.Coalition)
                || string.IsNullOrWhiteSpace(right.Coalition))
            {
                return false;
            }

            return left.Coalition.Equals(
                right.Coalition,
                StringComparison.OrdinalIgnoreCase);
        }

        private static TacviewLifecycleSample? GetNearestSampleNearTime(
            TacviewLifecycleObject lifecycleObject,
            double timeSeconds)
        {
            TacviewLifecycleSample? bestSample = null;
            double bestDelta = double.MaxValue;

            foreach (TacviewLifecycleSample sample in lifecycleObject.Samples)
            {
                double delta = Math.Abs(timeSeconds - sample.TimeSeconds);

                if (delta < bestDelta)
                {
                    bestSample = sample;
                    bestDelta = delta;
                }
            }

            if (bestDelta <= 1.0)
            {
                return bestSample;
            }

            return lifecycleObject.Start ?? lifecycleObject.End;
        }

        private static double DistanceMeters(
            TacviewLifecycleSample left,
            TacviewLifecycleSample right)
        {
            double dx = left.LocalX!.Value - right.LocalX!.Value;
            double dy = left.LocalY!.Value - right.LocalY!.Value;

            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static TacviewCorrelationConfidence GetLauncherConfidence(
            double? distanceMeters)
        {
            if (distanceMeters is null)
            {
                return TacviewCorrelationConfidence.Unknown;
            }

            if (distanceMeters.Value <= 100.0)
            {
                return TacviewCorrelationConfidence.High;
            }

            if (distanceMeters.Value <= 250.0)
            {
                return TacviewCorrelationConfidence.Medium;
            }

            return TacviewCorrelationConfidence.Low;
        }
    }
}