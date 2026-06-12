using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class TacviewWeaponTerminalCorrelator
    {
        private const double SimultaneousRemovalWindowSeconds = 0.05;
        private const double TerminalProximityWindowSeconds = 0.50;
        private const double MaxTerminalTargetDistanceMeters = 250.0;

        public static IReadOnlyList<TacviewWeaponTerminalEvent> Correlate(
            TacviewLifecycleReplay replay,
            IReadOnlyList<TacviewWeaponLaunch> launches)
        {
            ArgumentNullException.ThrowIfNull(replay);
            ArgumentNullException.ThrowIfNull(launches);

            Dictionary<string, TacviewWeaponLaunch> launchesByWeaponId = launches
                .ToDictionary(
                    l => l.WeaponObjectId,
                    StringComparer.OrdinalIgnoreCase);

            List<TacviewWeaponTerminalEvent> terminalEvents = new();

            foreach (TacviewWeaponLaunch launch in launches
                .OrderBy(l => l.LaunchTimeSeconds)
                .ThenBy(l => l.WeaponObjectId, StringComparer.OrdinalIgnoreCase))
            {
                if (!replay.Objects.TryGetValue(
                        launch.WeaponObjectId,
                        out TacviewLifecycleObject? weapon))
                {
                    continue;
                }

                if (weapon.End is null)
                {
                    continue;
                }

                double terminalTime = weapon.RemovedSeconds
                    ?? weapon.End.TimeSeconds;

                TacviewLifecycleSample terminalSample = weapon.End;

                TacviewLifecycleObject? target =
                    FindSimultaneouslyRemovedTarget(
                        replay,
                        weapon,
                        terminalTime,
                        terminalSample,
                        out double? targetDistanceMeters);

                TacviewCorrelationMethod method;
                TacviewTerminalOutcome outcome;
                bool destroyedTarget;

                if (target is not null)
                {
                    method = TacviewCorrelationMethod.SimultaneousRemoval;
                    outcome = TacviewTerminalOutcome.Kill;
                    destroyedTarget = true;
                }
                else
                {
                    target = FindNearestTerminalTarget(
                        replay,
                        weapon,
                        terminalTime,
                        terminalSample,
                        out targetDistanceMeters);

                    method = target is null
                        ? TacviewCorrelationMethod.Unknown
                        : TacviewCorrelationMethod.TerminalProximity;

                    outcome = target is null
                        ? TacviewTerminalOutcome.Miss
                        : TacviewTerminalOutcome.Hit;

                    destroyedTarget = false;
                }

                terminalEvents.Add(new TacviewWeaponTerminalEvent
                {
                    WeaponObjectId = launch.WeaponObjectId,
                    WeaponName = launch.WeaponName,
                    WeaponType = launch.WeaponType,

                    LauncherObjectId = launch.LauncherObjectId,
                    LauncherName = launch.LauncherName,
                    LauncherPilot = launch.LauncherPilot,
                    LauncherGroup = launch.LauncherGroup,

                    TargetObjectId = target?.ObjectId,
                    TargetName = target?.Name,
                    TargetPilot = target?.Pilot,
                    TargetGroup = target?.Group,
                    TargetType = target?.Type,

                    TerminalTimeSeconds = terminalTime,
                    TerminalSample = terminalSample,
                    TargetDistanceMeters = targetDistanceMeters,

                    Outcome = outcome,
                    DestroyedTarget = destroyedTarget,
                    CorrelationMethod = method,
                    Confidence = target is null
                        ? TacviewCorrelationConfidence.Unknown
                        : GetTargetConfidence(targetDistanceMeters),
                    LauncherConfidence = launch.Confidence,
                    TargetConfidence = target is null
                        ? TacviewCorrelationConfidence.Unknown
                        : GetTargetConfidence(targetDistanceMeters)
                });
            }

            return terminalEvents;
        }

        private static TacviewLifecycleObject? FindSimultaneouslyRemovedTarget(
            TacviewLifecycleReplay replay,
            TacviewLifecycleObject weapon,
            double terminalTime,
            TacviewLifecycleSample terminalSample,
            out double? targetDistanceMeters)
        {
            targetDistanceMeters = null;

            TacviewLifecycleObject? nearest = null;
            double nearestDistance = double.MaxValue;

            foreach (TacviewObjectRemoval removal in replay.Removals)
            {
                if (removal.ObjectId.Equals(weapon.ObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Math.Abs(removal.TimeSeconds - terminalTime) > SimultaneousRemovalWindowSeconds)
                {
                    continue;
                }

                if (!replay.Objects.TryGetValue(
                        removal.ObjectId,
                        out TacviewLifecycleObject? candidate))
                {
                    continue;
                }

                if (!IsPlausibleTarget(candidate, weapon))
                {
                    continue;
                }

                TacviewLifecycleSample? candidateSample = removal.LastSample ?? candidate.End;

                if (!HasLocalPosition(candidateSample) || !HasLocalPosition(terminalSample))
                {
                    continue;
                }

                double distance = DistanceMeters(terminalSample, candidateSample!);

                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            if (nearest is null || nearestDistance > MaxTerminalTargetDistanceMeters)
            {
                return null;
            }

            targetDistanceMeters = nearestDistance;
            return nearest;
        }

        private static TacviewLifecycleObject? FindNearestTerminalTarget(
            TacviewLifecycleReplay replay,
            TacviewLifecycleObject weapon,
            double terminalTime,
            TacviewLifecycleSample terminalSample,
            out double? targetDistanceMeters)
        {
            targetDistanceMeters = null;

            if (!HasLocalPosition(terminalSample))
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

                if (!IsPlausibleTarget(candidate, weapon))
                {
                    continue;
                }

                TacviewLifecycleSample? sample =
                    GetNearestSampleNearTime(candidate, terminalTime);

                if (!HasLocalPosition(sample))
                {
                    continue;
                }

                double timeDelta = Math.Abs(sample!.TimeSeconds - terminalTime);

                if (timeDelta > TerminalProximityWindowSeconds)
                {
                    continue;
                }

                double distance = DistanceMeters(terminalSample, sample);

                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            if (nearest is null || nearestDistance > MaxTerminalTargetDistanceMeters)
            {
                return null;
            }

            targetDistanceMeters = nearestDistance;
            return nearest;
        }

        private static bool IsPlausibleTarget(
            TacviewLifecycleObject candidate,
            TacviewLifecycleObject weapon)
        {
            if (candidate.IsWeapon)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(candidate.Coalition)
                || string.IsNullOrWhiteSpace(weapon.Coalition))
            {
                return true;
            }

            return !candidate.Coalition.Equals(
                weapon.Coalition,
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

            return bestSample ?? lifecycleObject.End ?? lifecycleObject.Start;
        }

        private static bool HasLocalPosition(TacviewLifecycleSample? sample)
        {
            return sample?.LocalX is not null
                && sample.LocalY is not null;
        }

        private static double DistanceMeters(
            TacviewLifecycleSample left,
            TacviewLifecycleSample right)
        {
            double dx = left.LocalX!.Value - right.LocalX!.Value;
            double dy = left.LocalY!.Value - right.LocalY!.Value;

            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static TacviewCorrelationConfidence GetTargetConfidence(
            double? distanceMeters)
        {
            if (distanceMeters is null)
            {
                return TacviewCorrelationConfidence.Unknown;
            }

            if (distanceMeters.Value <= 125.0)
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