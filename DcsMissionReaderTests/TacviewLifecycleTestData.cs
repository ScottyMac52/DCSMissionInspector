using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    internal static class TacviewLifecycleTestData
    {
        public const string RealCarrierBattleFileName =
            "Tacview-20260610-013423-DCS.txt.acmi";

        public static string RealCarrierBattlePath =>
            Path.Combine(
                AppContext.BaseDirectory,
                "TestData",
                RealCarrierBattleFileName);

        public static TacviewLifecycleReplay BuildRealCarrierBattleReplay()
        {
            Assert.True(
                File.Exists(RealCarrierBattlePath),
                $"Missing test ACMI file: {RealCarrierBattlePath}");

            using StreamReader reader = File.OpenText(RealCarrierBattlePath);

            return TacviewLifecycleReplayBuilder.Build(reader);
        }

        public static TacviewCombatReport BuildRealCarrierBattleCombatReport()
        {
            TacviewLifecycleReplay replay = BuildRealCarrierBattleReplay();

            IReadOnlyList<TacviewWeaponLaunch> launches =
                TacviewWeaponLaunchDetector.Detect(replay);

            IReadOnlyList<TacviewWeaponTerminalEvent> terminalEvents =
                TacviewWeaponTerminalCorrelator.Correlate(replay, launches);

            return TacviewCombatReportBuilder.Build(replay, launches, terminalEvents);
        }
    }
}