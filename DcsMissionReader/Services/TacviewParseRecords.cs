using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal sealed record TacviewAcmiParseData(
        TacviewMissionInfo Mission,
        IReadOnlyDictionary<string, TacviewObjectTrack> Objects,
        IReadOnlyList<TacviewEventRecord> Events,
        IReadOnlyList<TacviewRemovalRecord> Removals,
        IReadOnlyList<TacviewHealthChangeRecord> HealthChanges,
        DateTimeOffset? ReferenceTimeUtc);

    internal sealed record TacviewRemovalRecord(
        string ObjectId,
        double TimeSeconds,
        DateTimeOffset? AbsoluteTimeUtc);

    internal sealed record TacviewHealthChangeRecord(
        string ObjectId,
        double PreviousHealth,
        double NewHealth,
        double TimeSeconds,
        DateTimeOffset? AbsoluteTimeUtc,
        TacviewPositionSample? Position);
}