using MiniBrowser.Core.Storage;

namespace MiniBrowser.Core.History;

public sealed record SessionHistoryEntry(
    DateTimeOffset TimestampUtc,
    int TabId,
    string CapsuleId,
    string CapsuleName,
    string Title,
    string Url);

public sealed class SessionHistoryService
{
    private readonly object _gate = new();
    private readonly LinkedList<SessionHistoryEntry> _entries = new();

    public void Record(int tabId, string capsuleId, string capsuleName, string? title, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return;

        lock (_gate)
        {
            var last = _entries.Last?.Value;
            if (last is not null && last.TabId == tabId && string.Equals(last.Url, url, StringComparison.Ordinal))
            {
                return;
            }

            _entries.AddLast(new SessionHistoryEntry(
                DateTimeOffset.UtcNow,
                tabId,
                capsuleId,
                capsuleName,
                string.IsNullOrWhiteSpace(title) ? url : title,
                url));

            while (_entries.Count > StoragePolicy.SessionHistoryLimit)
            {
                _entries.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<SessionHistoryEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.Reverse().ToArray();
        }
    }
}
