using System.Text.Json.Serialization;

namespace MiniBrowser.Core.Machine;

public static class MachineProtocol
{
    public const int CurrentVersion = 2;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MachineDecisionType
{
    Allow,
    Block,
    Blur,
    Replace,
    Defer,
    ScanMore
}

public sealed record MachineDecision(
    string ResourceId,
    MachineDecisionType Decision,
    double Risk = 0,
    double Confidence = 1,
    int CacheForSeconds = 0)
{
    public static MachineDecision Allow(string resourceId = "") =>
        new(resourceId, MachineDecisionType.Allow);
}

public sealed record MachineEventEnvelope(
    string Event,
    int Version,
    Guid SessionId,
    Guid WindowId,
    int TabId,
    string CapsuleId,
    string FrameId,
    DateTimeOffset TimestampUtc,
    long MonotonicUs,
    string CausalId,
    IReadOnlyDictionary<string, object?> Payload);
