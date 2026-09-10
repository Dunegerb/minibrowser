namespace MiniBrowser.Core.Audit;

public sealed record NetworkAuditEntry(
    DateTimeOffset TimestampUtc,
    long MonotonicUs,
    Guid SessionId,
    Guid WindowId,
    int TabId,
    string CapsuleId,
    string FrameId,
    ulong RequestId,
    string Method,
    string Domain,
    string UrlSha256,
    string ResourceType,
    string Initiator,
    bool IsNavigation,
    bool IsDownload,
    string PolicyAction,
    string PolicyReason);
