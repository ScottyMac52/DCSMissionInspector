using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal enum TacviewTargetDomain
    {
        Unknown,
        Air,
        Sea,
        Ground,
        Static,
        Weapon
    }

    internal enum TacviewWeaponRole
    {
        Unknown,
        DefensiveInterceptor,
        OffensiveStrikeWeapon,
        AirToAir,
        SurfaceToAir,
        AirToSurface,
        SurfaceToSurface
    }

    internal static class TacviewCombatClassifier
    {
        private const double EarthRadiusMeters = 6_371_000.0;

        public static TacviewTargetDomain GetTargetDomain(TacviewObjectTrack track)
        {
            ArgumentNullException.ThrowIfNull(track);

            if (track.IsWeapon)
            {
                return TacviewTargetDomain.Weapon;
            }

            string typeText = track.Type ?? string.Empty;

            // Tacview Type is the highest-confidence source.
            // Do this before looking at Name or Group, because a group like
            // "Carrier Killer Group" does not mean the object is a carrier.
            if (ContainsAny(typeText, "Sea", "Watercraft", "AircraftCarrier", "Ship", "Destroyer", "Cruiser", "Frigate", "Submarine"))
            {
                return TacviewTargetDomain.Sea;
            }

            if (ContainsAny(typeText, "Air", "FixedWing", "Rotorcraft", "Aircraft", "Helicopter"))
            {
                return TacviewTargetDomain.Air;
            }

            if (ContainsAny(typeText, "Ground", "Vehicle", "Armor", "Tank", "Infantry", "Artillery"))
            {
                return TacviewTargetDomain.Ground;
            }

            if (ContainsAny(typeText, "Static", "Building", "Structure", "Fortification"))
            {
                return TacviewTargetDomain.Static;
            }

            string nameText = track.Name ?? string.Empty;
            string groupText = track.Group ?? string.Empty;
            string fallbackText = string.Join(' ', nameText, groupText);

            // Lower-confidence fallback for cases where Tacview Type is missing/poor.
            if (ContainsAny(fallbackText, "CVN", "CG-", "DDG-", "FFG-", "AircraftCarrier", "Destroyer", "Cruiser", "Frigate", "Submarine", "Ship"))
            {
                return TacviewTargetDomain.Sea;
            }

            if (ContainsAny(fallbackText, "AWACS", "Overlord", "Rotary", "Helicopter"))
            {
                return TacviewTargetDomain.Air;
            }

            return TacviewTargetDomain.Unknown;
        }

        public static TacviewWeaponRole GetWeaponRole(
            TacviewObjectTrack weapon,
            TacviewObjectTrack? launcher)
        {
            ArgumentNullException.ThrowIfNull(weapon);

            if (!weapon.IsWeapon)
            {
                return TacviewWeaponRole.Unknown;
            }

            string weaponText = CombineClassificationText(weapon);
            TacviewTargetDomain launcherDomain = launcher is null
                ? TacviewTargetDomain.Unknown
                : GetTargetDomain(launcher);

            if (IsDefensiveInterceptor(weapon, launcher))
            {
                return TacviewWeaponRole.DefensiveInterceptor;
            }

            if (ContainsAny(weaponText, "AIM-", "AIM_", "AirToAir", "Air-to-Air"))
            {
                return TacviewWeaponRole.AirToAir;
            }

            if (ContainsAny(weaponText, "SAM", "SurfaceToAir", "Surface-to-Air"))
            {
                return TacviewWeaponRole.SurfaceToAir;
            }

            if (launcherDomain == TacviewTargetDomain.Sea
                && ContainsAny(weaponText, "Missile", "Weapon"))
            {
                return TacviewWeaponRole.SurfaceToSurface;
            }

            if (launcherDomain == TacviewTargetDomain.Air
                && ContainsAny(weaponText, "Bomb", "Rocket", "AGM", "Kh", "X_", "Missile"))
            {
                return TacviewWeaponRole.AirToSurface;
            }

            if (IsOffensiveStrikeWeapon(weapon, launcher))
            {
                return TacviewWeaponRole.OffensiveStrikeWeapon;
            }

            return TacviewWeaponRole.Unknown;
        }

        public static bool IsSeaTarget(TacviewObjectTrack track)
        {
            return GetTargetDomain(track) == TacviewTargetDomain.Sea;
        }

        public static bool IsAirTarget(TacviewObjectTrack track)
        {
            return GetTargetDomain(track) == TacviewTargetDomain.Air;
        }

        public static bool IsWeaponTarget(TacviewObjectTrack track)
        {
            return GetTargetDomain(track) == TacviewTargetDomain.Weapon;
        }

        public static bool IsDefensiveInterceptor(
            TacviewObjectTrack weapon,
            TacviewObjectTrack? launcher)
        {
            ArgumentNullException.ThrowIfNull(weapon);

            if (!weapon.IsWeapon)
            {
                return false;
            }

            string weaponText = CombineClassificationText(weapon);
            TacviewTargetDomain launcherDomain = launcher is null
                ? TacviewTargetDomain.Unknown
                : GetTargetDomain(launcher);

            if (ContainsAny(
                    weaponText,
                    "SM_2",
                    "SM-2",
                    "SM_2ER",
                    "SM-2ER",
                    "RIM",
                    "SeaSparrow",
                    "Sea Sparrow",
                    "ESSM",
                    "SAM",
                    "AIM-",
                    "AIM_"))
            {
                return true;
            }

            return false;
        }

        public static bool IsOffensiveStrikeWeapon(
            TacviewObjectTrack weapon,
            TacviewObjectTrack? launcher)
        {
            ArgumentNullException.ThrowIfNull(weapon);

            if (!weapon.IsWeapon)
            {
                return false;
            }

            string weaponText = CombineClassificationText(weapon);
            TacviewTargetDomain launcherDomain = launcher is null
                ? TacviewTargetDomain.Unknown
                : GetTargetDomain(launcher);

            if (ContainsAny(
                    weaponText,
                    "X_",
                    "Kh",
                    "AGM",
                    "AntiShip",
                    "Anti-Ship",
                    "Cruise",
                    "Bomb",
                    "Rocket"))
            {
                return true;
            }

            return launcherDomain == TacviewTargetDomain.Air
                && ContainsAny(weaponText, "Missile", "Bomb", "Rocket", "Weapon");
        }

        public static double CalculateHorizontalDistanceMeters(
            TacviewPositionSample first,
            TacviewPositionSample second)
        {
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);

            double firstLatitudeRadians = DegreesToRadians(first.Latitude);
            double secondLatitudeRadians = DegreesToRadians(second.Latitude);
            double latitudeDeltaRadians = DegreesToRadians(second.Latitude - first.Latitude);
            double longitudeDeltaRadians = DegreesToRadians(second.Longitude - first.Longitude);

            double a =
                Math.Sin(latitudeDeltaRadians / 2.0) * Math.Sin(latitudeDeltaRadians / 2.0)
                + Math.Cos(firstLatitudeRadians)
                * Math.Cos(secondLatitudeRadians)
                * Math.Sin(longitudeDeltaRadians / 2.0)
                * Math.Sin(longitudeDeltaRadians / 2.0);

            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return EarthRadiusMeters * c;
        }

        public static double CalculateVerticalDistanceMeters(
            TacviewPositionSample first,
            TacviewPositionSample second)
        {
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);

            return Math.Abs((first.AltitudeMeters ?? 0.0) - (second.AltitudeMeters ?? 0.0));
        }

        public static double CalculateDistance3dMeters(
            TacviewPositionSample first,
            TacviewPositionSample second)
        {
            double horizontalMeters = CalculateHorizontalDistanceMeters(first, second);
            double verticalMeters = CalculateVerticalDistanceMeters(first, second);

            return Math.Sqrt(
                horizontalMeters * horizontalMeters
                + verticalMeters * verticalMeters);
        }

        private static string CombineClassificationText(TacviewObjectTrack track)
        {
            return string.Join(
                ' ',
                track.Name,
                track.Type,
                track.Group);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            return terms.Any(term =>
                value.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}