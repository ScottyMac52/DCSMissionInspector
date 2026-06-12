using System;
using System.Collections.Generic;

namespace DcsMissionReader.Models
{
    public enum TacviewCorrelationMethod
    {
        Unknown = 0,
        ExplicitEvent = 1,
        ParentLink = 2,
        BirthProximity = 3,
        TerminalProximity = 4,
        SimultaneousRemoval = 5
    }

    public enum TacviewCorrelationConfidence
    {
        Unknown = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum TacviewTerminalOutcome
    {
        Unknown = 0,
        Hit = 1,
        Kill = 2,
        Miss = 3,
        Intercepted = 4,
        Expired = 5
    }

    public sealed class TacviewLifecycleReplay
    {
        public Dictionary<string, TacviewLifecycleObject> Objects { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<TacviewLifecycleFrame> Frames { get; } = new();

        public List<TacviewWeaponBirth> WeaponBirths { get; } = new();

        public List<TacviewObjectRemoval> Removals { get; } = new();
    }

    public sealed class TacviewLifecycleFrame
    {
        public double TimeSeconds { get; init; }

        public List<TacviewLifecycleObjectUpdate> Updates { get; } = new();

        public List<TacviewObjectRemoval> Removals { get; } = new();
    }

    public sealed class TacviewLifecycleObjectUpdate
    {
        public string ObjectId { get; init; } = string.Empty;

        public double TimeSeconds { get; init; }

        public string RawLine { get; init; } = string.Empty;

        public TacviewLifecycleSample? Sample { get; init; }
    }

    public sealed class TacviewLifecycleObject
    {
        public string ObjectId { get; init; } = string.Empty;

        public string? Name { get; set; }

        public string? Pilot { get; set; }

        public string? Group { get; set; }

        public string? Type { get; set; }

        public string? Color { get; set; }

        public string? Coalition { get; set; }

        public string? Country { get; set; }

        public double? FirstSeenSeconds { get; set; }

        public double? LastSeenSeconds { get; set; }

        public double? RemovedSeconds { get; set; }

        public TacviewLifecycleSample? Start { get; set; }

        public TacviewLifecycleSample? End { get; set; }

        public List<TacviewLifecycleSample> Samples { get; } = new();

        public bool IsWeapon =>
     !string.IsNullOrWhiteSpace(Type)
     && (Type.Contains("Weapon", StringComparison.OrdinalIgnoreCase)
         || Type.Contains("Projectile", StringComparison.OrdinalIgnoreCase)
         || Type.Contains("Shell", StringComparison.OrdinalIgnoreCase));
    }

    public sealed class TacviewLifecycleSample
    {
        public string ObjectId { get; init; } = string.Empty;

        public double TimeSeconds { get; init; }

        public double? LongitudeOffset { get; init; }

        public double? LatitudeOffset { get; init; }

        public double? AltitudeMeters { get; init; }

        public double? LocalX { get; init; }

        public double? LocalY { get; init; }

        public double? HeadingDegrees { get; init; }

        public string RawTransform { get; init; } = string.Empty;

        public double? X => LocalX;

        public double? Y => LocalY;

        public double? Longitude => LongitudeOffset;

        public double? Latitude => LatitudeOffset;

        public double? Altitude => AltitudeMeters;
    }

    public sealed class TacviewObjectRemoval
    {
        public string ObjectId { get; init; } = string.Empty;

        public double TimeSeconds { get; init; }

        public string? ObjectName { get; init; }

        public string? ObjectPilot { get; init; }

        public string? ObjectGroup { get; init; }

        public string? ObjectType { get; init; }

        public TacviewLifecycleSample? LastSample { get; init; }
    }

    public sealed class TacviewWeaponBirth
    {
        public string WeaponObjectId { get; init; } = string.Empty;

        public string? WeaponName { get; init; }

        public string? WeaponType { get; init; }

        public string? WeaponCoalition { get; init; }

        public string? WeaponCountry { get; init; }

        public double TimeSeconds { get; init; }

        public TacviewLifecycleSample? BirthSample { get; init; }
    }

    public sealed class TacviewWeaponLaunch
    {
        public string WeaponObjectId { get; init; } = string.Empty;

        public string? WeaponName { get; init; }

        public string? WeaponType { get; init; }

        public string? WeaponCoalition { get; init; }

        public string? WeaponCountry { get; init; }

        public double LaunchTimeSeconds { get; init; }

        public TacviewLifecycleSample? LaunchSample { get; init; }

        public string? LauncherObjectId { get; init; }

        public string? LauncherName { get; init; }

        public string? LauncherPilot { get; init; }

        public string? LauncherGroup { get; init; }

        public string? LauncherType { get; init; }

        public string? LauncherCoalition { get; init; }

        public string? LauncherCountry { get; init; }

        public double? LauncherDistanceMeters { get; init; }

        public TacviewCorrelationMethod CorrelationMethod { get; init; }

        public TacviewCorrelationConfidence Confidence { get; init; }
    }

    public sealed class TacviewWeaponTerminalEvent
    {
        public string WeaponObjectId { get; init; } = string.Empty;

        public string? WeaponName { get; init; }

        public string? WeaponType { get; init; }

        public string? LauncherObjectId { get; init; }

        public string? LauncherName { get; init; }

        public string? LauncherPilot { get; init; }

        public string? LauncherGroup { get; init; }

        public string? TargetObjectId { get; init; }

        public string? TargetName { get; init; }

        public string? TargetPilot { get; init; }

        public string? TargetGroup { get; init; }

        public string? TargetType { get; init; }

        public double TerminalTimeSeconds { get; init; }

        public TacviewLifecycleSample? TerminalSample { get; init; }

        public double? TargetDistanceMeters { get; init; }

        public TacviewTerminalOutcome Outcome { get; init; }

        public bool DestroyedTarget { get; init; }

        public TacviewCorrelationMethod CorrelationMethod { get; init; }

        public TacviewCorrelationConfidence Confidence { get; init; }

        public TacviewCorrelationConfidence LauncherConfidence { get; init; }

        public TacviewCorrelationConfidence TargetConfidence { get; init; }
    }

    public sealed class TacviewCombatReport
    {
        public List<TacviewWeaponLaunch> WeaponLaunches { get; } = new();

        public List<TacviewWeaponTerminalEvent> TerminalEvents { get; } = new();

        public List<TacviewTargetCombatSummary> Targets { get; } = new();
    }

    public sealed class TacviewTargetCombatSummary
    {
        public string TargetObjectId { get; init; } = string.Empty;

        public string? TargetName { get; init; }

        public string? TargetPilot { get; init; }

        public string? TargetGroup { get; init; }

        public string? TargetType { get; init; }

        public int HitCount { get; init; }

        public bool Destroyed { get; init; }

        public double? DestroyedAtSeconds { get; init; }

        public string? KillingWeaponObjectId { get; init; }

        public string? KillingWeaponName { get; init; }

        public string? KillingLauncherObjectId { get; init; }

        public string? KillingLauncherName { get; init; }

        public List<TacviewWeaponTerminalEvent> Hits { get; } = new();
    }
}