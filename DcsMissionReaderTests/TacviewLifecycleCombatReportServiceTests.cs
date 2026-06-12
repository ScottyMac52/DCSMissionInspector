using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class TacviewLifecycleCombatReportServiceTests
    {
        [Fact]
        public void BuildFromAcmiReader_WithRealAcmi_BuildsExpectedCarrierCombatReport()
        {
            using StreamReader reader =
                File.OpenText(TacviewLifecycleTestData.RealCarrierBattlePath);

            TacviewCombatReport report =
                TacviewLifecycleCombatReportService.BuildFromAcmiReader(reader);

            TacviewTargetCombatSummary cvn73 = Assert.Single(
                report.Targets.Where(t =>
                    t.TargetObjectId == "301"
                    || string.Equals(t.TargetName, "CVN_73", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.TargetPilot, "Washington", StringComparison.OrdinalIgnoreCase)));

            Assert.Equal("301", cvn73.TargetObjectId);
            Assert.Equal("CVN_73", cvn73.TargetName);
            Assert.Equal("Washington", cvn73.TargetPilot);

            Assert.True(cvn73.Destroyed);
            Assert.Equal(3, cvn73.HitCount);

            Assert.Equal("26401", cvn73.KillingWeaponObjectId);
            Assert.Equal("P_700", cvn73.KillingWeaponName);
            Assert.Equal("201", cvn73.KillingLauncherObjectId);
            Assert.Equal("PIOTR", cvn73.KillingLauncherName);

            Assert.Contains(
                cvn73.Hits,
                h => h.WeaponObjectId == "1e301"
                     && h.Outcome == TacviewTerminalOutcome.Hit);

            Assert.Contains(
                cvn73.Hits,
                h => h.WeaponObjectId == "25801"
                     && h.Outcome == TacviewTerminalOutcome.Hit);

            Assert.Contains(
                cvn73.Hits,
                h => h.WeaponObjectId == "26401"
                     && h.Outcome == TacviewTerminalOutcome.Kill);
        }
    }
}