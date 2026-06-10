using DcsMissionReader.Models;
using DcsMissionReader.Services;

namespace DcsMissionReaderTests
{
    public class TacviewCombatClassifierTests
    {
        [Fact]
        public void GetTargetDomain_WithAircraftCarrier_ReturnsSea()
        {
            TacviewObjectTrack track = new()
            {
                ObjectId = "101",
                Name = "CVN_73",
                Type = "Sea+Watercraft+AircraftCarrier",
                Group = "Washington CSG"
            };

            Assert.Equal(
                TacviewTargetDomain.Sea,
                TacviewCombatClassifier.GetTargetDomain(track));
        }

        [Fact]
        public void GetTargetDomain_WithRotorcraft_ReturnsAir()
        {
            TacviewObjectTrack track = new()
            {
                ObjectId = "301",
                Name = "SH-60B",
                Type = "Air+Rotorcraft",
                Group = "Rotary-1"
            };

            Assert.Equal(
                TacviewTargetDomain.Air,
                TacviewCombatClassifier.GetTargetDomain(track));
        }

        [Fact]
        public void GetTargetDomain_WithWeaponMissile_ReturnsWeapon()
        {
            TacviewObjectTrack track = new()
            {
                ObjectId = "901",
                Name = "X_22",
                Type = "Weapon+Missile",
                Group = "Carrier Killer Group"
            };

            Assert.Equal(
                TacviewTargetDomain.Weapon,
                TacviewCombatClassifier.GetTargetDomain(track));
        }

        [Fact]
        public void GetWeaponRole_WithShipLaunchedSm2_ReturnsDefensiveInterceptor()
        {
            TacviewObjectTrack launcher = new()
            {
                ObjectId = "102",
                Name = "USS Truxtun DDG-103",
                Type = "Sea+Watercraft+Destroyer",
                Group = "DDG Astern"
            };

            TacviewObjectTrack weapon = new()
            {
                ObjectId = "1001",
                Name = "SM_2",
                Type = "Weapon+Missile",
                Group = "DDG Astern",
                ParentObjectId = "102"
            };

            Assert.Equal(
                TacviewWeaponRole.DefensiveInterceptor,
                TacviewCombatClassifier.GetWeaponRole(weapon, launcher));
        }

        [Fact]
        public void GetWeaponRole_WithAirLaunchedX22_ReturnsOffensiveStrikeWeapon()
        {
            TacviewObjectTrack launcher = new()
            {
                ObjectId = "201",
                Name = "Tu-22M3",
                Type = "Air+FixedWing",
                Group = "Carrier Killer Group"
            };

            TacviewObjectTrack weapon = new()
            {
                ObjectId = "901",
                Name = "X_22",
                Type = "Weapon+Missile",
                Group = "Carrier Killer Group",
                ParentObjectId = "201"
            };

            Assert.Equal(
                TacviewWeaponRole.AirToSurface,
                TacviewCombatClassifier.GetWeaponRole(weapon, launcher));

            Assert.True(
                TacviewCombatClassifier.IsOffensiveStrikeWeapon(weapon, launcher));
        }

        [Fact]
        public void CalculateDistance3dMeters_WithSameLatLonAndAltitudeDifference_ReturnsVerticalDistance()
        {
            TacviewPositionSample first = CreateSample(
                latitude: 25.0,
                longitude: 57.0,
                altitudeMeters: 0.0);

            TacviewPositionSample second = CreateSample(
                latitude: 25.0,
                longitude: 57.0,
                altitudeMeters: 1_000.0);

            Assert.Equal(
                1_000.0,
                TacviewCombatClassifier.CalculateDistance3dMeters(first, second),
                precision: 3);
        }

        [Fact]
        public void CalculateDistance3dMeters_WithLargeAltitudeDifference_IsNotSurfaceOnlyDistance()
        {
            TacviewPositionSample carrier = CreateSample(
                latitude: 25.53163180,
                longitude: 57.17663780,
                altitudeMeters: 0.0);

            TacviewPositionSample highMissile = CreateSample(
                latitude: 25.53163180,
                longitude: 57.17663780,
                altitudeMeters: 4_572.0); // 15,000 feet-ish.

            double horizontalDistance = TacviewCombatClassifier.CalculateHorizontalDistanceMeters(
                carrier,
                highMissile);

            double distance3d = TacviewCombatClassifier.CalculateDistance3dMeters(
                carrier,
                highMissile);

            Assert.Equal(0.0, horizontalDistance, precision: 3);
            Assert.Equal(4_572.0, distance3d, precision: 3);
        }

        private static TacviewPositionSample CreateSample(
            double latitude,
            double longitude,
            double altitudeMeters)
        {
            return new TacviewPositionSample
            {
                TimeSeconds = 0.0,
                AbsoluteTimeUtc = DateTimeOffset.Parse("2016-06-21T04:30:00Z"),
                Latitude = latitude,
                Longitude = longitude,
                AltitudeMeters = altitudeMeters
            };
        }
    }
}