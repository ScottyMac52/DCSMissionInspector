using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class TacviewCombatReportBuilder
    {
        public static TacviewCombatReport Build(
            TacviewLifecycleReplay replay,
            IReadOnlyList<TacviewWeaponLaunch> launches,
            IReadOnlyList<TacviewWeaponTerminalEvent> terminalEvents)
        {
            ArgumentNullException.ThrowIfNull(replay);
            ArgumentNullException.ThrowIfNull(launches);
            ArgumentNullException.ThrowIfNull(terminalEvents);

            var report = new TacviewCombatReport();

            report.WeaponLaunches.AddRange(
                launches.OrderBy(l => l.LaunchTimeSeconds)
                    .ThenBy(l => l.WeaponObjectId, StringComparer.OrdinalIgnoreCase));

            report.TerminalEvents.AddRange(
                terminalEvents.OrderBy(e => e.TerminalTimeSeconds)
                    .ThenBy(e => e.WeaponObjectId, StringComparer.OrdinalIgnoreCase));

            foreach (IGrouping<string?, TacviewWeaponTerminalEvent> targetGroup in terminalEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.TargetObjectId))
                .GroupBy(e => e.TargetObjectId, StringComparer.OrdinalIgnoreCase))
            {
                string targetObjectId = targetGroup.Key!;

                TacviewLifecycleObject? target = replay.Objects.TryGetValue(
                    targetObjectId,
                    out TacviewLifecycleObject? foundTarget)
                    ? foundTarget
                    : null;

                List<TacviewWeaponTerminalEvent> hits = targetGroup
                    .Where(e =>
                        e.Outcome == TacviewTerminalOutcome.Hit
                        || e.Outcome == TacviewTerminalOutcome.Kill)
                    .OrderBy(e => e.TerminalTimeSeconds)
                    .ThenBy(e => e.WeaponObjectId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (hits.Count == 0)
                {
                    continue;
                }

                TacviewWeaponTerminalEvent? kill = hits
                    .FirstOrDefault(h => h.Outcome == TacviewTerminalOutcome.Kill || h.DestroyedTarget);

                var summary = new TacviewTargetCombatSummary
                {
                    TargetObjectId = targetObjectId,
                    TargetName = target?.Name ?? hits[0].TargetName,
                    TargetPilot = target?.Pilot ?? hits[0].TargetPilot,
                    TargetGroup = target?.Group ?? hits[0].TargetGroup,
                    TargetType = target?.Type ?? hits[0].TargetType,

                    HitCount = hits.Count,
                    Destroyed = kill is not null,
                    DestroyedAtSeconds = kill?.TerminalTimeSeconds,

                    KillingWeaponObjectId = kill?.WeaponObjectId,
                    KillingWeaponName = kill?.WeaponName,
                    KillingLauncherObjectId = kill?.LauncherObjectId,
                    KillingLauncherName = kill?.LauncherName
                };

                summary.Hits.AddRange(hits);

                report.Targets.Add(summary);
            }

            report.Targets.Sort(
                static (left, right) =>
                {
                    int destroyedCompare = right.Destroyed.CompareTo(left.Destroyed);

                    if (destroyedCompare != 0)
                    {
                        return destroyedCompare;
                    }

                    int hitCountCompare = right.HitCount.CompareTo(left.HitCount);

                    if (hitCountCompare != 0)
                    {
                        return hitCountCompare;
                    }

                    return string.Compare(
                        left.TargetObjectId,
                        right.TargetObjectId,
                        StringComparison.OrdinalIgnoreCase);
                });

            return report;
        }
    }
}