using CefSharp;
using System.IO;

namespace MiniBrowser.Core.Capsules;

/// <summary>
/// Owns browser identity/storage compartments. A RequestContext is shared only by tabs in the
/// same capsule. Empty CachePath intentionally creates a CEF incognito/in-memory context.
/// </summary>
public sealed class CapsuleManager : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RequestContext> _contexts = new(StringComparer.Ordinal);

    public CapsuleDescriptor Anonymous { get; }
    public CapsuleDescriptor Personal { get; }
    public IReadOnlyList<CapsuleDescriptor> AvailableCapsules { get; }

    public CapsuleManager(string appRoot)
    {
        var root = GetRootCachePath(appRoot);
        Directory.CreateDirectory(root);

        var anonymousId = $"anon-{Guid.NewGuid():N}";
        Anonymous = new CapsuleDescriptor(
            anonymousId[..17],
            "Anônima",
            CapsuleKind.Anonymous,
            null);

        Personal = new CapsuleDescriptor(
            "personal",
            "Pessoal",
            CapsuleKind.Personal,
            Path.Combine(root, "capsules", "personal"));

        AvailableCapsules = new[] { Anonymous, Personal };
    }

    public static string GetRootCachePath(string appRoot) => Path.Combine(appRoot, "cef-root");

    public static string GetGlobalCachePath(string appRoot) => Path.Combine(GetRootCachePath(appRoot), "global");

    public CapsuleDescriptor GetById(string capsuleId) =>
        AvailableCapsules.First(capsule => string.Equals(capsule.Id, capsuleId, StringComparison.Ordinal));

    public IRequestContext GetRequestContext(CapsuleDescriptor capsule)
    {
        lock (_gate)
        {
            if (_contexts.TryGetValue(capsule.Id, out var existing)) return existing;

            if (!capsule.IsEphemeral)
            {
                Directory.CreateDirectory(capsule.CachePath!);
            }

            var settings = new RequestContextSettings
            {
                CachePath = capsule.CachePath ?? string.Empty,
                PersistSessionCookies = !capsule.IsEphemeral,
                AcceptLanguageList = "pt-BR,pt,en-US,en"
            };

            var context = new RequestContext(settings);
            _contexts.Add(capsule.Id, context);
            return context;
        }
    }

    public void ClearHttpCache(CapsuleDescriptor capsule)
    {
        lock (_gate)
        {
            if (_contexts.TryGetValue(capsule.Id, out var context) && !context.IsDisposed)
            {
                context.ClearHttpCache(null!);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var context in _contexts.Values)
            {
                try { context.Dispose(); } catch { }
            }
            _contexts.Clear();
        }
    }
}
