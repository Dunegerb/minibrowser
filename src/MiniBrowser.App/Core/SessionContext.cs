using System.Diagnostics;

namespace MiniBrowser.Core;

public sealed class SessionContext
{
    private readonly long _startTimestamp;

    public Guid SessionId { get; }
    public Guid WindowId { get; }
    public DateTimeOffset StartedUtc { get; }

    private SessionContext(Guid sessionId, Guid windowId, DateTimeOffset startedUtc, long startTimestamp)
    {
        SessionId = sessionId;
        WindowId = windowId;
        StartedUtc = startedUtc;
        _startTimestamp = startTimestamp;
    }

    public static SessionContext Create() =>
        new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, Stopwatch.GetTimestamp());

    public long MonotonicMicroseconds()
    {
        var delta = Stopwatch.GetTimestamp() - _startTimestamp;
        return (long)(delta * 1_000_000d / Stopwatch.Frequency);
    }
}
