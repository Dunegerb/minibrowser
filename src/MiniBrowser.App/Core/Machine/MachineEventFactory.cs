namespace MiniBrowser.Core.Machine;

public static class MachineEventFactory
{
    public static MachineEventEnvelope SearchBeforeSubmit(SessionContext session, int tabId, string query) =>
        Create(session, tabId, string.Empty, "search.before_submit", new Dictionary<string, object?>
        {
            ["query"] = query
        });

    public static MachineEventEnvelope RelapseConfirmed(SessionContext session, int tabId) =>
        Create(session, tabId, string.Empty, "relapse.confirmed", new Dictionary<string, object?>());

    public static MachineEventEnvelope ResourceRequest(
        SessionContext session,
        int tabId,
        string frameId,
        string resourceId,
        string resourceType,
        string domain,
        string urlSha256,
        string method,
        string initiator,
        bool isNavigation,
        bool isDownload) =>
        Create(session, tabId, frameId, "network.resource.request", new Dictionary<string, object?>
        {
            ["resource_id"] = resourceId,
            ["resource_type"] = resourceType,
            ["domain"] = domain,
            ["url_hash"] = urlSha256,
            ["method"] = method,
            ["initiator"] = initiator,
            ["is_navigation"] = isNavigation,
            ["is_download"] = isDownload
        });

    private static MachineEventEnvelope Create(
        SessionContext session,
        int tabId,
        string frameId,
        string eventName,
        IReadOnlyDictionary<string, object?> payload) =>
        new(
            eventName,
            1,
            session.SessionId,
            session.WindowId,
            tabId,
            frameId,
            DateTimeOffset.UtcNow,
            session.MonotonicMicroseconds(),
            Guid.NewGuid().ToString("N"),
            payload);
}
