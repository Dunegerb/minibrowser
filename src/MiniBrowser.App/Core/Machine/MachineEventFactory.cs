namespace MiniBrowser.Core.Machine;

public static class MachineEventFactory
{
    public static MachineEventEnvelope SearchBeforeSubmit(SessionContext session, int tabId, string capsuleId, string query) =>
        Create(session, tabId, capsuleId, string.Empty, "search.before_submit", new Dictionary<string, object?>
        {
            ["query"] = query
        });

    public static MachineEventEnvelope RelapseConfirmed(SessionContext session, int tabId, string capsuleId) =>
        Create(session, tabId, capsuleId, string.Empty, "relapse.confirmed", new Dictionary<string, object?>());

    public static MachineEventEnvelope ResourceRequest(
        SessionContext session,
        int tabId,
        string capsuleId,
        string frameId,
        string resourceId,
        string resourceType,
        string domain,
        string urlSha256,
        string method,
        string initiator,
        bool isNavigation,
        bool isDownload,
        string policyAction,
        string policyReason) =>
        Create(session, tabId, capsuleId, frameId, "network.resource.request", new Dictionary<string, object?>
        {
            ["resource_id"] = resourceId,
            ["resource_type"] = resourceType,
            ["domain"] = domain,
            ["url_hash"] = urlSha256,
            ["method"] = method,
            ["initiator"] = initiator,
            ["is_navigation"] = isNavigation,
            ["is_download"] = isDownload,
            ["policy_action"] = policyAction,
            ["policy_reason"] = policyReason
        });

    private static MachineEventEnvelope Create(
        SessionContext session,
        int tabId,
        string capsuleId,
        string frameId,
        string eventName,
        IReadOnlyDictionary<string, object?> payload) =>
        new(
            eventName,
            MachineProtocol.CurrentVersion,
            session.SessionId,
            session.WindowId,
            tabId,
            capsuleId,
            frameId,
            DateTimeOffset.UtcNow,
            session.MonotonicMicroseconds(),
            Guid.NewGuid().ToString("N"),
            payload);
}
