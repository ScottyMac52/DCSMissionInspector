namespace DcsMissionReader.Models
{
    public sealed class WeaponResultInferenceOptions
    {
        #region Fields

        public const string SectionName = "PostBriefing:WeaponResultInference";
        private const double SameTimeRemovalWindowDefaultSeconds = 0.25;
        private const double HealthDropMaxDamageDistanceDefaultMeters = 5_000.0;
        private const double HealthDropMaxWeaponTimeBeforeDamageDefaultSeconds = 20.0;
        private const double HealthDropMaxWeaponTimeAfterDamageDefaultSeconds = 3.0;
        private const double UnpairedRemovalMaxInferredDamageDistanceDefaultMeters = 3_000.0;
        private const double UnpairedRemovalMaxTargetSampleTimeDifferenceDefaultSeconds = 15.0;
        private const double DefensivePairMaxTimeDifferenceDefaultSeconds = 0.75;
        private const double DefensivePairMaxDistanceDefaultMeters = 750.0;
        private const bool EnableTerminalProximityDamageInferenceDefault = false;
        private const bool EnableTerminalProximityNearMissReportingDefault = false;

        #endregion Fields

        #region Properties 

        public bool EnableTerminalProximityNearMissReporting { get; init; } = EnableTerminalProximityNearMissReportingDefault;

        public bool EnableTerminalProximityDamageInference { get; init; } = EnableTerminalProximityDamageInferenceDefault;

        public double SameTimeRemovalWindowSeconds { get; init; } = SameTimeRemovalWindowDefaultSeconds;

        public double HealthDropMaxDamageDistanceMeters { get; init; } = HealthDropMaxDamageDistanceDefaultMeters;

        public double HealthDropMaxWeaponTimeBeforeDamageSeconds { get; init; } = HealthDropMaxWeaponTimeBeforeDamageDefaultSeconds;

        public double HealthDropMaxWeaponTimeAfterDamageSeconds { get; init; } = HealthDropMaxWeaponTimeAfterDamageDefaultSeconds;

        public double UnpairedRemovalMaxInferredDamageDistanceMeters { get; init; } = UnpairedRemovalMaxInferredDamageDistanceDefaultMeters;

        public double UnpairedRemovalMaxTargetSampleTimeDifferenceSeconds { get; init; } = UnpairedRemovalMaxTargetSampleTimeDifferenceDefaultSeconds;

        public double DefensivePairMaxTimeDifferenceSeconds { get; init; } = DefensivePairMaxTimeDifferenceDefaultSeconds;

        public double DefensivePairMaxDistanceMeters { get; init; } = DefensivePairMaxDistanceDefaultMeters;

        #endregion Properties
    }
}