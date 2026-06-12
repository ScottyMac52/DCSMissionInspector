using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public sealed class TacviewLifecycleReplayBuilderTests
    {
        [Fact]
        public void Build_WithRealAcmi_CreatesInitialObjectIdentitiesAndTracksUpdates()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            Assert.True(replay.Frames.Count > 80_000);
            Assert.Equal(2601.96, replay.Frames[^1].TimeSeconds, precision: 2);

            TacviewLifecycleObject piotr = replay.Objects["201"];

            Assert.Equal("201", piotr.ObjectId);
            Assert.Equal("PIOTR", piotr.Name);
            Assert.Equal("Pyotr Velikiy", piotr.Pilot);
            Assert.Equal("Kuznetsov Strike Group Escort", piotr.Group);
            Assert.Equal("Sea+Watercraft+Warship", piotr.Type);
            Assert.Equal("Allies", piotr.Coalition);
            Assert.Equal("ru", piotr.Country);
            Assert.Equal(0.0, piotr?.FirstSeenSeconds ?? 0.0, precision: 2);
            Assert.Null(piotr.RemovedSeconds);
            Assert.True(piotr.Samples.Count > 1_000);

            TacviewLifecycleObject cvn73 = replay.Objects["301"];

            Assert.Equal("301", cvn73.ObjectId);
            Assert.Equal("CVN_73", cvn73.Name);
            Assert.Equal("Washington", cvn73.Pilot);
            Assert.Equal("Washington CSG", cvn73.Group);
            Assert.Equal("Sea+Watercraft+AircraftCarrier", cvn73.Type);
            Assert.Equal("Enemies", cvn73.Coalition);
            Assert.Equal("us", cvn73.Country);
            Assert.Equal(0.0, cvn73?.FirstSeenSeconds ?? 0.0, precision: 2);
            Assert.Equal(987.23, cvn73.RemovedSeconds!.Value, precision: 2);
            Assert.Equal(987.23, cvn73.End!.TimeSeconds, precision: 2);
        }

        [Fact]
        public void Build_WithRealAcmi_TreatsFirstWeaponIdentityLineAsWeaponBirth()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            TacviewLifecycleObject p700 = replay.Objects["26401"];

            Assert.True(p700.IsWeapon);
            Assert.Equal("26401", p700.ObjectId);
            Assert.Equal("P_700", p700.Name);
            Assert.Equal("Weapon+Missile", p700.Type);
            Assert.Equal("Allies", p700.Coalition);
            Assert.Equal("ru", p700.Country);

            Assert.Equal(822.85, p700?.FirstSeenSeconds ?? 0, precision: 2);
            Assert.Equal(987.23, p700.RemovedSeconds!.Value, precision: 2);

            Assert.NotNull(p700.Start);
            Assert.Equal(822.85, p700.Start!.TimeSeconds, precision: 2);
            Assert.Equal(4.6824229, p700?.Start?.LongitudeOffset ?? 0.0, precision: 7);
            Assert.Equal(5.2850205, p700?.Start?.LatitudeOffset ?? 0.0, precision: 7);
            Assert.Equal(12.0, p700.Start.AltitudeMeters!.Value, precision: 2);
            Assert.Equal(44050.61, p700.Start.LocalX!.Value, precision: 2);
            Assert.Equal(12354.59, p700.Start.LocalY!.Value, precision: 2);

            Assert.NotNull(p700.End);
            Assert.Equal(987.23, p700.End!.TimeSeconds, precision: 2);
        }

        [Fact]
        public void Build_WithRealAcmi_DoesNotTreatLaterWeaponSamplesAsNewLaunches()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            IReadOnlyList<TacviewWeaponBirth> p700Births = replay.WeaponBirths
                .Where(b => b.WeaponObjectId == "26401")
                .ToList();

            TacviewWeaponBirth birth = Assert.Single(p700Births);

            Assert.Equal("26401", birth.WeaponObjectId);
            Assert.Equal("P_700", birth.WeaponName);
            Assert.Equal(822.85, birth.TimeSeconds, precision: 2);

            Assert.True(
                replay.Objects["26401"].Samples.Count > 10,
                "The weapon should have many trajectory samples, but only one birth.");
        }

        [Fact]
        public void Build_WithRealAcmi_RecordsObjectRemovalsFromMinusObjectIdLines()
        {
            TacviewLifecycleReplay replay =
                TacviewLifecycleTestData.BuildRealCarrierBattleReplay();

            Assert.Contains(
                replay.Removals,
                r => r.ObjectId == "301"
                     && Math.Abs(r.TimeSeconds - 987.23) < 0.01);

            Assert.Contains(
                replay.Removals,
                r => r.ObjectId == "26401"
                     && Math.Abs(r.TimeSeconds - 987.23) < 0.01);

            Assert.Equal(987.23, replay.Objects["301"].RemovedSeconds!.Value, precision: 2);
            Assert.Equal(987.23, replay.Objects["26401"].RemovedSeconds!.Value, precision: 2);
        }
    }
}