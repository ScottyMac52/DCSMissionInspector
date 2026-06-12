using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class TacviewCombatReportBuilderTests
    {
        [Fact]
        public void Build_WithRealAcmi_ReportsCvn73WasHitThreeTimesAndDestroyedOnThirdHit()
        {
            TacviewCombatReport report =
                TacviewLifecycleTestData.BuildRealCarrierBattleCombatReport();

            TacviewTargetCombatSummary cvn73 = Assert.Single(
                report.Targets.Where(t => t.TargetObjectId == "301"));

            Assert.Equal("CVN_73", cvn73.TargetName);
            Assert.Equal("Washington", cvn73.TargetPilot);
            Assert.Equal("Washington CSG", cvn73.TargetGroup);

            Assert.Equal(3, cvn73.HitCount);
            Assert.True(cvn73.Destroyed);
            Assert.Equal(987.23, cvn73.DestroyedAtSeconds!.Value, precision: 2);

            Assert.Equal("26401", cvn73.KillingWeaponObjectId);
            Assert.Equal("P_700", cvn73.KillingWeaponName);
            Assert.Equal("201", cvn73.KillingLauncherObjectId);
            Assert.Equal("PIOTR", cvn73.KillingLauncherName);

            Assert.Collection(
                cvn73.Hits.OrderBy(h => h.TerminalTimeSeconds),
                first =>
                {
                    Assert.Equal("1e301", first.WeaponObjectId);
                    Assert.Equal(TacviewTerminalOutcome.Hit, first.Outcome);
                    Assert.False(first.DestroyedTarget);
                },
                second =>
                {
                    Assert.Equal("25801", second.WeaponObjectId);
                    Assert.Equal(TacviewTerminalOutcome.Hit, second.Outcome);
                    Assert.False(second.DestroyedTarget);
                },
                third =>
                {
                    Assert.Equal("26401", third.WeaponObjectId);
                    Assert.Equal(TacviewTerminalOutcome.Kill, third.Outcome);
                    Assert.True(third.DestroyedTarget);
                });
        }

        [Fact]
        public void Build_WithRealAcmi_ReportsPiotrAsLauncherForAllP700Cvn73Hits()
        {
            TacviewCombatReport report =
                TacviewLifecycleTestData.BuildRealCarrierBattleCombatReport();

            TacviewTargetCombatSummary cvn73 = Assert.Single(
                report.Targets.Where(t => t.TargetObjectId == "301"));

            Assert.All(
                cvn73.Hits,
                hit =>
                {
                    Assert.Equal("P_700", hit.WeaponName);
                    Assert.Equal("201", hit.LauncherObjectId);
                    Assert.Equal("PIOTR", hit.LauncherName);
                    Assert.Equal(TacviewCorrelationConfidence.High, hit.LauncherConfidence);
                });
        }
    }
}