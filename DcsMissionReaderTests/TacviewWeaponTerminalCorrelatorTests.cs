using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class TacviewWeaponTerminalCorrelatorTests
    {
        [Fact]
        public void Correlate_WithRealAcmi_FindsThreeP700TerminalEventsAgainstCvn73()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            IReadOnlyList<TacviewWeaponLaunch> launches =
                TacviewWeaponLaunchDetector.Detect(replay);

            IReadOnlyList<TacviewWeaponTerminalEvent> terminalEvents =
                TacviewWeaponTerminalCorrelator.Correlate(replay, launches);

            IReadOnlyList<TacviewWeaponTerminalEvent> cvn73Hits = terminalEvents
                .Where(e => e.TargetObjectId == "301")
                .OrderBy(e => e.TerminalTimeSeconds)
                .ToList();

            Assert.Equal(3, cvn73Hits.Count);

            Assert.Collection(
                cvn73Hits,
                first =>
                {
                    Assert.Equal("1e301", first.WeaponObjectId);
                    Assert.Equal("P_700", first.WeaponName);
                    Assert.Equal("201", first.LauncherObjectId);
                    Assert.Equal(799.24, first.TerminalTimeSeconds, precision: 2);
                    Assert.Equal(TacviewTerminalOutcome.Hit, first.Outcome);
                    Assert.False(first.DestroyedTarget);
                    Assert.Equal(TacviewCorrelationMethod.TerminalProximity, first.CorrelationMethod);
                },
                second =>
                {
                    Assert.Equal("25801", second.WeaponObjectId);
                    Assert.Equal("P_700", second.WeaponName);
                    Assert.Equal("201", second.LauncherObjectId);
                    Assert.Equal(916.34, second.TerminalTimeSeconds, precision: 2);
                    Assert.Equal(TacviewTerminalOutcome.Hit, second.Outcome);
                    Assert.False(second.DestroyedTarget);
                    Assert.Equal(TacviewCorrelationMethod.TerminalProximity, second.CorrelationMethod);
                },
                third =>
                {
                    Assert.Equal("26401", third.WeaponObjectId);
                    Assert.Equal("P_700", third.WeaponName);
                    Assert.Equal("201", third.LauncherObjectId);
                    Assert.Equal(987.23, third.TerminalTimeSeconds, precision: 2);
                    Assert.Equal(TacviewTerminalOutcome.Kill, third.Outcome);
                    Assert.True(third.DestroyedTarget);
                    Assert.Equal(TacviewCorrelationMethod.SimultaneousRemoval, third.CorrelationMethod);
                });
        }

        [Fact]
        public void Correlate_WithRealAcmi_MarksCvn73KillWhenWeaponAndTargetAreRemovedTogether()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            IReadOnlyList<TacviewWeaponLaunch> launches =
                TacviewWeaponLaunchDetector.Detect(replay);

            IReadOnlyList<TacviewWeaponTerminalEvent> terminalEvents =
                TacviewWeaponTerminalCorrelator.Correlate(replay, launches);

            TacviewWeaponTerminalEvent killShot = Assert.Single(
                terminalEvents.Where(e =>
                    e.WeaponObjectId == "26401"
                    && e.TargetObjectId == "301"));

            Assert.Equal("P_700", killShot.WeaponName);
            Assert.Equal("PIOTR", killShot.LauncherName);
            Assert.Equal("CVN_73", killShot.TargetName);
            Assert.Equal("Washington", killShot.TargetPilot);
            Assert.Equal("Washington CSG", killShot.TargetGroup);

            Assert.Equal(987.23, killShot.TerminalTimeSeconds, precision: 2);
            Assert.Equal(TacviewTerminalOutcome.Kill, killShot.Outcome);
            Assert.True(killShot.DestroyedTarget);
            Assert.Equal(TacviewCorrelationConfidence.High, killShot.Confidence);
        }
    }
}