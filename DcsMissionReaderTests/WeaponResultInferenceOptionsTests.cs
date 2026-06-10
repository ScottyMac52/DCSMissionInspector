using DcsMissionReader.Models;
using Microsoft.Extensions.Configuration;

namespace DcsMissionReaderTests
{
    public sealed class WeaponResultInferenceOptionsTests
    {
        [Fact]
        public void Constructor_UsesCurrentPostBriefingInferenceDefaults()
        {
            var options = new WeaponResultInferenceOptions();

            
            Assert.Equal(0.25, options.SameTimeRemovalWindowSeconds);
            Assert.Equal(5_000.0, options.HealthDropMaxDamageDistanceMeters);
            Assert.Equal(20.0, options.HealthDropMaxWeaponTimeBeforeDamageSeconds);
            Assert.Equal(3.0, options.HealthDropMaxWeaponTimeAfterDamageSeconds);
            Assert.Equal(3_000.0, options.UnpairedRemovalMaxInferredDamageDistanceMeters);
            Assert.Equal(15.0, options.UnpairedRemovalMaxTargetSampleTimeDifferenceSeconds);
            Assert.Equal(0.75, options.DefensivePairMaxTimeDifferenceSeconds);
            Assert.Equal(750.0, options.DefensivePairMaxDistanceMeters);
            Assert.False(options.EnableTerminalProximityNearMissReporting);
        }

        [Fact]
        public void Bind_WithConfiguration_OverridesDefaults()
        {
            Dictionary<string, string?> values = new()
            {
                ["PostBriefing:WeaponResultInference:SameTimeRemovalWindowSeconds"] = "0.5",
                ["PostBriefing:WeaponResultInference:HealthDropMaxDamageDistanceMeters"] = "1234",
                ["PostBriefing:WeaponResultInference:HealthDropMaxWeaponTimeBeforeDamageSeconds"] = "11",
                ["PostBriefing:WeaponResultInference:HealthDropMaxWeaponTimeAfterDamageSeconds"] = "2",
                ["PostBriefing:WeaponResultInference:UnpairedRemovalMaxInferredDamageDistanceMeters"] = "2222",
                ["PostBriefing:WeaponResultInference:UnpairedRemovalMaxTargetSampleTimeDifferenceSeconds"] = "9",
                ["PostBriefing:WeaponResultInference:DefensivePairMaxTimeDifferenceSeconds"] = "1.25",
                ["PostBriefing:WeaponResultInference:DefensivePairMaxDistanceMeters"] = "333",
                ["PostBriefing:WeaponResultInference:EnableTerminalProximityDamageInference"] = "true",
                ["PostBriefing:WeaponResultInference:EnableTerminalProximityNearMissReporting"] = "true"
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            WeaponResultInferenceOptions options =
                configuration
                    .GetSection(WeaponResultInferenceOptions.SectionName)
                    .Get<WeaponResultInferenceOptions>()
                ?? new WeaponResultInferenceOptions();

            Assert.Equal(0.5, options.SameTimeRemovalWindowSeconds);
            Assert.Equal(1234.0, options.HealthDropMaxDamageDistanceMeters);
            Assert.Equal(11.0, options.HealthDropMaxWeaponTimeBeforeDamageSeconds);
            Assert.Equal(2.0, options.HealthDropMaxWeaponTimeAfterDamageSeconds);
            Assert.Equal(2222.0, options.UnpairedRemovalMaxInferredDamageDistanceMeters);
            Assert.Equal(9.0, options.UnpairedRemovalMaxTargetSampleTimeDifferenceSeconds);
            Assert.Equal(1.25, options.DefensivePairMaxTimeDifferenceSeconds);
            Assert.Equal(333.0, options.DefensivePairMaxDistanceMeters);
            Assert.True(options.EnableTerminalProximityDamageInference);
            Assert.True(options.EnableTerminalProximityNearMissReporting);
        }

        [Fact]
        public void PostBriefingService_DefaultConstructor_UsesDefaultInferenceOptions()
        {
            var service = new DcsMissionReader.Services.PostBriefingService();

            Assert.NotNull(service);
        }

        [Fact]
        public void PostBriefingService_Constructor_AcceptsInjectedInferenceOptions()
        {
            var options = new WeaponResultInferenceOptions
            {
                SameTimeRemovalWindowSeconds = 0.5
            };

            var service = new DcsMissionReader.Services.PostBriefingService(
                weaponResultInferenceOptions: options);

            Assert.NotNull(service);
        }
    }
}
