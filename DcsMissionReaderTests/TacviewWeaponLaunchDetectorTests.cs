using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class TacviewWeaponLaunchDetectorTests
    {
        [Fact]
        public void Detect_WithRealAcmi_CountsWeaponBirthsByWeaponName()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            IReadOnlyList<TacviewWeaponLaunch> launches =
                TacviewWeaponLaunchDetector.Detect(replay);

            Dictionary<string, int> counts = launches
                .GroupBy(l => l.WeaponName)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            Assert.Equal(401, launches.Count);

            Assert.Equal(159, counts["SM_2"]);
            Assert.Equal(133, counts["SM_2ER"]);
            Assert.Equal(24, counts["X_22"]);
            Assert.Equal(20, counts["P_700"]);
            Assert.Equal(20, counts["SA48H6E2"]);
            Assert.Equal(19, counts["weapons.shells.M61_20_HE_gr"]);
            Assert.Equal(8, counts["AGM_84S"]);
            Assert.Equal(6, counts["SeaSparrow"]);
            Assert.Equal(6, counts["weapons.shells.M61_20_AP_gr"]);
            Assert.Equal(3, counts["RIM_116A"]);
            Assert.Equal(2, counts["P_40R"]);
            Assert.Equal(1, counts["P_33E"]);
        }

        [Fact]
        public void Detect_WithRealAcmi_InfersP70026401WasLaunchedByPiotr()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            IReadOnlyList<TacviewWeaponLaunch> launches =
                TacviewWeaponLaunchDetector.Detect(replay);

            TacviewWeaponLaunch launch = Assert.Single(
                launches.Where(l => l.WeaponObjectId == "26401"));

            Assert.Equal("P_700", launch.WeaponName);
            Assert.Equal(822.85, launch.LaunchTimeSeconds, precision: 2);

            Assert.Equal("201", launch.LauncherObjectId);
            Assert.Equal("PIOTR", launch.LauncherName);
            Assert.Equal("Pyotr Velikiy", launch.LauncherPilot);
            Assert.Equal("Kuznetsov Strike Group Escort", launch.LauncherGroup);

            Assert.Equal(
                TacviewCorrelationMethod.BirthProximity,
                launch.CorrelationMethod);

            Assert.Equal(
                TacviewCorrelationConfidence.High,
                launch.Confidence);

            Assert.True(
                launch.LauncherDistanceMeters <= 50.0,
                $"Expected PIOTR to be within 50m of P_700 birth, actual distance was {launch.LauncherDistanceMeters:0.0}m.");
        }

        [Fact]
        public void Detect_WithRealAcmi_InfersX22LaunchersFromTu22BirthProximity()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            IReadOnlyList<TacviewWeaponLaunch> launches =
                TacviewWeaponLaunchDetector.Detect(replay);

            IReadOnlyList<TacviewWeaponLaunch> x22Launches = launches
                .Where(l => l.WeaponName == "X_22")
                .ToList();

            Assert.Equal(24, x22Launches.Count);

            Assert.All(
                x22Launches,
                launch =>
                {
                    Assert.StartsWith("Tu-22M3", launch.LauncherName);
                    Assert.Equal(TacviewCorrelationMethod.BirthProximity, launch.CorrelationMethod);
                    Assert.Equal(TacviewCorrelationConfidence.High, launch.Confidence);
                    Assert.True(launch.LauncherDistanceMeters <= 100.0);
                });
        }
    }
}